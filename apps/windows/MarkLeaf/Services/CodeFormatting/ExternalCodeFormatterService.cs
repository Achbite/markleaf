using System.Diagnostics;
using System.Text;
using MarkLeaf.Services.Logging;

namespace MarkLeaf.Services.CodeFormatting;

public sealed record CodeFormatterOutcome(
    bool Success,
    string? FormattedCode,
    string? ErrorMessage);

/// <summary>
/// 执行外部代码格式化器：代码经 stdin 传入（UTF-8 无 BOM），退出码 0 视为
/// 成功并回传 stdout。整体超时 15 秒，与编辑器侧的 formatter 等待上限一致。
/// </summary>
public sealed class ExternalCodeFormatterService
{
    public const int DefaultTimeoutSeconds = 15;

    private readonly IAppLogger _logger;

    public ExternalCodeFormatterService(IAppLogger logger)
    {
        _logger = logger;
    }

    public CodeFormatterOutcome Format(
        ExternalCodeFormatterTool tool,
        string code,
        string toolPath,
        IReadOnlyDictionary<string, string> toolPaths,
        string sqlDialect,
        int? startLine = null,
        int? endLine = null,
        CancellationToken cancellationToken = default)
    {
        var arguments = new List<string>(tool.StdinArguments);
        if (tool.Id == "sqlfluff")
        {
            // sqlfluff 的子命令必须放在 stdin 占位符之前：format --dialect x -。
            arguments.InsertRange(0, ["format", "--dialect", sqlDialect]);
        }
        else if (tool.SelectionLineRangePrefix is { } prefix
            && startLine is { } from
            && endLine is { } to
            && to >= from)
        {
            arguments.Insert(0, $"{prefix}{from}:{to}");
        }

        var isJar = toolPath.EndsWith(".jar", StringComparison.OrdinalIgnoreCase);
        var fileName = isJar ? ResolveJavaExecutable() : toolPath;
        if (string.IsNullOrEmpty(fileName) || (!isJar && !File.Exists(fileName)))
        {
            return new CodeFormatterOutcome(false, null, toolPath);
        }

        if (isJar)
        {
            arguments.Insert(0, toolPath);
            arguments.Insert(0, "-jar");
        }

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = QuoteArguments(arguments),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                    StandardErrorEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                },
                EnableRaisingEvents = false,
            };

            if (!process.Start())
            {
                return new CodeFormatterOutcome(false, null, "process start failed");
            }

            // stdin 写入与输出读取并发进行，避免管道缓冲区写满后互相等待死锁。
            var stdinTask = Task.Run(async () =>
            {
                await using var stream = process.StandardInput;
                await stream.WriteAsync(code.AsMemory(), cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Close();
            }, CancellationToken.None);

            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            var timeoutMilliseconds = DefaultTimeoutSeconds * 1000;
            if (!process.WaitForExit(timeoutMilliseconds))
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                return new CodeFormatterOutcome(false, null, "timeout");
            }

            Task.WaitAll([stdinTask, outputTask, errorTask], CancellationToken.None);
            if (process.ExitCode != 0)
            {
                var error = FirstLine(errorTask.Result);
                _logger.Warning($"External formatter '{tool.Id}' exited with {process.ExitCode}: {error}");
                return new CodeFormatterOutcome(false, null, error);
            }

            return new CodeFormatterOutcome(true, outputTask.Result, null);
        }
        catch (Exception exception) when (
            exception is IOException
                or System.ComponentModel.Win32Exception
                or InvalidOperationException
                or UnauthorizedAccessException)
        {
            _logger.Warning($"External formatter '{tool.Id}' failed to run: {exception.Message}");
            return new CodeFormatterOutcome(false, null, exception.Message);
        }
    }

    public static string FirstLine(string text)
    {
        var span = text.AsSpan().Trim();
        var newline = span.IndexOfAny('\r', '\n');
        return newline < 0 ? span.ToString() : span[..newline].Trim().ToString();
    }

    private static string QuoteArguments(IReadOnlyList<string> arguments)
    {
        // Windows 命令行 quoting：含空格或引号的参数整体加引号，内部引号翻倍。
        return string.Join(
            ' ',
            arguments.Select(argument =>
            {
                if (argument.Length == 0 || argument.Contains(' ') || argument.Contains('"'))
                {
                    return $"\"{argument.Replace("\"", "\\\"")}\"";
                }

                return argument;
            }));
    }

    private static string? ResolveJavaExecutable()
    {
        var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (!string.IsNullOrWhiteSpace(javaHome))
        {
            var candidate = Path.Combine(javaHome, "bin", "java.exe");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        var pathVariable = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var directory in pathVariable.Split(
            Path.PathSeparator,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            try
            {
                if (!Path.IsPathRooted(directory))
                {
                    continue;
                }

                var candidate = Path.Combine(directory, "java.exe");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            catch (ArgumentException)
            {
            }
        }

        return null;
    }
}
