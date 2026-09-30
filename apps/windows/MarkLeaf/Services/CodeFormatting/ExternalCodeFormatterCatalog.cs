namespace MarkLeaf.Services.CodeFormatting;

public enum ExternalCodeFormatterAvailability
{
    Available,
    NotInstalled,
    InvalidCustomPath,
}

public sealed record ExternalCodeFormatterTool(
    string Id,
    string DisplayName,
    IReadOnlyList<string> Languages,
    string ExecutableName,
    IReadOnlyList<string> StdinArguments,
    string? SelectionLineRangePrefix = null,
    IReadOnlyList<string>? ProbeArgumentsOverride = null)
{
    /// <summary>gofmt 没有 --version，用 -h 探测；其余工具统一 --version。</summary>
    public IReadOnlyList<string> ProbeArguments => ProbeArgumentsOverride ?? DefaultProbeArguments;

    private static readonly IReadOnlyList<string> DefaultProbeArguments = ["--version"];
}

/// <summary>
/// 外部代码格式化器目录，与 apps/macos 的 ExternalCodeFormatterCatalog 保持
/// 同一工具清单与语言映射；Windows 侧探测顺序为自定义路径 → PATH → 常见
/// 包管理器回退目录（scoop shims / chocolatey bin）。
/// </summary>
public static class ExternalCodeFormatterCatalog
{
    public static IReadOnlyList<ExternalCodeFormatterTool> Tools { get; } =
    [
        new("black", "Black", ["python", "py"], "black", ["--stdin-filename", "input.py", "-"]),
        new("google-java-format", "google-java-format", ["java"], "google-java-format", ["-"], SelectionLineRangePrefix: "--lines="),
        new("clang-format", "Clang-Format", ["c", "cpp", "c++", "cc", "cxx", "hpp", "objective-c", "objc", "m", "mm"], "clang-format", ["--assume-filename=input.c"]),
        new("verible-verilog-format", "Verible", ["verilog", "sv", "systemverilog"], "verible-verilog-format", ["-"]),
        new("latexindent", "latexindent", ["latex", "tex"], "latexindent", ["-"]),
        new("rustfmt", "rustfmt", ["rust", "rs"], "rustfmt", ["--emit", "stdout"]),
        new("gofmt", "gofmt", ["go"], "gofmt", [], ProbeArgumentsOverride: ["-h"]),
        new("sqlfluff", "sqlfluff", ["sql"], "sqlfluff", ["-"]),
        new("shfmt", "shfmt", ["shell", "sh", "bash"], "shfmt", []),
        new("taplo", "Taplo", ["toml"], "taplo", ["format", "-"]),
        new("xmllint", "xmllint", ["xml"], "xmllint", ["--format", "-"]),
    ];

    public static string? SelectionLineRangePrefix(string toolId) =>
        Tools.FirstOrDefault(tool => tool.Id == toolId)?.SelectionLineRangePrefix;

    public static ExternalCodeFormatterTool? ToolForLanguage(string language)
    {
        var normalized = language.Trim().ToLowerInvariant();
        return Tools.FirstOrDefault(tool => tool.Languages.Contains(normalized, StringComparer.Ordinal));
    }

    public static IReadOnlyList<string> AvailableLanguages(IReadOnlyDictionary<string, string> toolPaths)
    {
        return Tools
            .Where(tool => ProbeAvailability(tool, toolPaths) == ExternalCodeFormatterAvailability.Available)
            .SelectMany(tool => tool.Languages)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    public static ExternalCodeFormatterAvailability ProbeAvailability(
        ExternalCodeFormatterTool tool,
        IReadOnlyDictionary<string, string> toolPaths)
    {
        if (ResolveExecutable(tool, toolPaths) is not null)
        {
            return ExternalCodeFormatterAvailability.Available;
        }

        // 配置了自定义路径但文件不存在：与"未安装"区分，提示用户修正路径。
        return toolPaths.TryGetValue(tool.Id, out var configured)
                && !string.IsNullOrWhiteSpace(configured)
            ? ExternalCodeFormatterAvailability.InvalidCustomPath
            : ExternalCodeFormatterAvailability.NotInstalled;
    }

    /// <summary>
    /// 解析可执行文件：名称含路径分隔符时直接使用；否则依次查找自定义路径、
    /// PATH 与包管理器回退目录。返回 null 表示不可用。
    /// </summary>
    public static string? ResolveExecutable(
        ExternalCodeFormatterTool tool,
        IReadOnlyDictionary<string, string> toolPaths)
    {
        if (toolPaths.TryGetValue(tool.Id, out var configured)
            && !string.IsNullOrWhiteSpace(configured))
        {
            if (configured.EndsWith(".jar", StringComparison.OrdinalIgnoreCase))
            {
                // google-java-format 以 jar 分发；自定义路径指向 jar 时经 java -jar 启动，
                // Java 运行时沿用 JAVA_HOME → PATH 的常规顺序。
                return File.Exists(configured) ? configured : null;
            }

            if (File.Exists(configured))
            {
                return configured;
            }

            return null;
        }

        var pathVariable = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var searchDirectories = pathVariable
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        searchDirectories.AddRange(FallbackDirectories());
        foreach (var directory in searchDirectories)
        {
            try
            {
                if (!Path.IsPathRooted(directory))
                {
                    continue;
                }

                var candidate = Path.Combine(directory, tool.ExecutableName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                // 包管理器在 Windows 上的 shim 通常是 .exe/.cmd/.bat。
                foreach (var extension in new[] { ".exe", ".cmd", ".bat" })
                {
                    candidate = Path.Combine(directory, tool.ExecutableName + extension);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }
            catch (Exception exception) when (
                exception is ArgumentException or IOException or UnauthorizedAccessException)
            {
                // 跳过不可读的 PATH 目录。
            }
        }

        return null;
    }

    private static IEnumerable<string> FallbackDirectories()
    {
        var directories = new List<string>();
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(userProfile))
        {
            directories.Add(Path.Combine(userProfile, "scoop", "shims"));
            directories.Add(Path.Combine(userProfile, ".local", "bin"));
            directories.Add(Path.Combine(userProfile, ".cargo", "bin"));
            directories.Add(Path.Combine(userProfile, "go", "bin"));
        }

        var commonProgramFiles = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        if (!string.IsNullOrEmpty(commonProgramFiles))
        {
            directories.Add(Path.Combine(commonProgramFiles, "chocolatey", "bin"));
        }

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (!string.IsNullOrEmpty(programFiles))
        {
            directories.Add(Path.Combine(programFiles, "Go", "bin"));
            directories.Add(Path.Combine(programFiles, "LLVM", "bin"));
        }

        return directories;
    }
}
