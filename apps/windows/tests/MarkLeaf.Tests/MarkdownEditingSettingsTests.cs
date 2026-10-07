using MarkLeaf.Editor;
using MarkLeaf.Services.Settings;
using System.Text.Json;

namespace MarkLeaf.Tests;

/// <summary>
/// 1.7.7 同步的 Markdown 编辑行为契约：setMarkdownEditingSettings 载荷必须
/// 携带 editor-core 约定的全部键（含新增的 codeBlockSpellcheck），且设置可持久化往返。
/// </summary>
[TestClass]
public sealed class MarkdownEditingSettingsTests
{
    [TestMethod]
    public void BuildPayload_IncludesEveryContractKey()
    {
        var settings = new EditorSettings
        {
            ExitBlockOnEmptyEnter = true,
            UseShiftEnterHardBreak = false,
            MarkdownCodeFence = "tilde",
            MarkdownEmphasisMarker = "underscore",
            MarkdownBulletMarker = "plus",
            EscapeLiteralSymbols = true,
            EscapeMarkdownLiteralSymbols = false,
            CodeBlockSpellcheck = true,
        };

        var payload = EditorHostController.BuildMarkdownEditingSettingsPayload(settings);
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;

        Assert.IsTrue(root.GetProperty("exitBlockOnEmptyEnter").GetBoolean());
        Assert.IsFalse(root.GetProperty("useShiftEnterHardBreak").GetBoolean());
        Assert.AreEqual("tilde", root.GetProperty("codeFence").GetString());
        Assert.AreEqual("underscore", root.GetProperty("emphasisMarker").GetString());
        Assert.AreEqual("plus", root.GetProperty("bulletMarker").GetString());
        Assert.IsTrue(root.GetProperty("escapeLiteralSymbols").GetBoolean());
        Assert.IsFalse(root.GetProperty("escapeMarkdownLiteralSymbols").GetBoolean());
        // 1.7.7 新增：代码块拼写检查默认关闭，开启时必须如实下发。
        Assert.IsTrue(root.GetProperty("codeBlockSpellcheck").GetBoolean());
    }

    [TestMethod]
    public void BuildPayload_DefaultsSpellcheckToOff()
    {
        var payload = EditorHostController.BuildMarkdownEditingSettingsPayload(new EditorSettings());

        using var document = JsonDocument.Parse(payload);

        Assert.IsFalse(document.RootElement.GetProperty("codeBlockSpellcheck").GetBoolean());
    }

    [TestMethod]
    public async Task Settings_RoundTripsCodeBlockSpellcheck()
    {
        var root = Path.Combine(Path.GetTempPath(), "markleaf-tests", Guid.NewGuid().ToString("N"));
        var file = Path.Combine(root, "settings.json");

        try
        {
            var service = new JsonSettingsService(file, new TestLogger());
            var expected = new AppSettings
            {
                Editor = new EditorSettings
                {
                    CodeBlockSpellcheck = true,
                },
            };

            await service.SaveAsync(expected);
            var actual = await service.LoadAsync();

            Assert.IsTrue(actual.Editor.CodeBlockSpellcheck);
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }

    [TestMethod]
    public async Task Settings_MissingSpellcheckKey_DefaultsToOff()
    {
        var root = Path.Combine(Path.GetTempPath(), "markleaf-tests", Guid.NewGuid().ToString("N"));
        var file = Path.Combine(root, "settings.json");
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(
            file,
            $$"""{ "schemaVersion": {{AppSettings.CurrentSchemaVersion}}, "editor": { "exitBlockOnEmptyEnter": true } }""");

        try
        {
            var service = new JsonSettingsService(file, new TestLogger());
            var actual = await service.LoadAsync();

            Assert.IsTrue(actual.Editor.ExitBlockOnEmptyEnter);
            Assert.IsFalse(actual.Editor.CodeBlockSpellcheck);
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }
}
