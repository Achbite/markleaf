using MarkLeaf.Editor;
using MarkLeaf.Services;
using MarkLeaf.Services.CodeFormatting;

namespace MarkLeaf.UI;

internal sealed partial class MainForm
{
    private void OnCodeFormatRequested(object? sender, CodeFormatRequest request)
    {
        var tool = ExternalCodeFormatterCatalog.ToolForLanguage(request.Language);
        if (tool is null)
        {
            _editorHost?.SendCodeFormatResult(
                request.RequestId, CodeFormatStatus.Failed, message: Loc.Get("codeFormatter.unsupportedLanguage"));
            return;
        }

        var toolPath = ExternalCodeFormatterCatalog.ResolveExecutable(tool, _settings.CodeFormatter.ToolPaths);
        if (toolPath is null)
        {
            _editorHost?.SendCodeFormatResult(
                request.RequestId, CodeFormatStatus.Failed, message: Loc.Format("codeFormatter.notInstalled", tool.DisplayName));
            return;
        }

        SetStatus(Loc.Get("codeFormatter.formatting"));
        var sqlDialect = _settings.CodeFormatter.SqlDialect;
        var toolPaths = _settings.CodeFormatter.ToolPaths;
        _ = Task.Run(() => _codeFormatterService.Format(
                tool, request.Code, toolPath, toolPaths, sqlDialect, request.StartLine, request.EndLine))
            .ContinueWith(
                task =>
                {
                    if (IsDisposed || !IsHandleCreated)
                    {
                        return;
                    }

                    BeginInvoke(() => CompleteCodeFormat(request, tool.Id, task));
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
    }

    private void CompleteCodeFormat(CodeFormatRequest request, string toolId, Task<CodeFormatterOutcome> task)
    {
        if (task.IsFaulted || task.Result is not { } outcome)
        {
            _logger.Error("External code formatting crashed.", task.Exception);
            _editorHost?.SendCodeFormatResult(
                request.RequestId, CodeFormatStatus.Failed, message: Loc.Get("codeFormatter.failed"));
            return;
        }

        if (!outcome.Success)
        {
            var reason = DescribeFormatterError(outcome.ErrorMessage);
            _editorHost?.SendCodeFormatResult(
                request.RequestId, CodeFormatStatus.Failed, message: reason);
            // 状态栏透出格式化器的原始诊断（如 black 的 Cannot parse 行:列），
            // 笼统的"失败"无法帮助用户定位语法问题。
            SetStatus(reason);
            return;
        }

        if (string.Equals(outcome.FormattedCode, request.Code, StringComparison.Ordinal))
        {
            _editorHost?.SendCodeFormatResult(request.RequestId, CodeFormatStatus.Unchanged);
            SetStatus(Loc.Get("codeFormatter.unchanged"));
            return;
        }

        _editorHost?.SendCodeFormatResult(request.RequestId, CodeFormatStatus.Formatted, code: outcome.FormattedCode);
        SetStatus(Loc.Get("codeFormatter.formatted"));
        _logger.Info($"External code formatted with {toolId}.");
    }

    /// <summary>把常见的外部失败原因映射为本地化提示，其余原样透出。</summary>
    private static string DescribeFormatterError(string? error)
    {
        if (string.IsNullOrWhiteSpace(error))
        {
            return Loc.Get("codeFormatter.failed");
        }

        var normalized = error.Trim();
        if (normalized.Contains("no java runtime", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("java runtime not found", StringComparison.OrdinalIgnoreCase))
        {
            return Loc.Get("codeFormatter.missingJava");
        }

        return normalized;
    }

    /// <summary>
    /// 把本机可用的格式化器语言并集下发给编辑器；会话建立与设置变更时都要调用。
    /// </summary>
    private void ApplyCodeFormatterSettings()
    {
        _editorHost?.SetCodeFormatterSettings(
            ExternalCodeFormatterCatalog.AvailableLanguages(_settings.CodeFormatter.ToolPaths));
    }
}
