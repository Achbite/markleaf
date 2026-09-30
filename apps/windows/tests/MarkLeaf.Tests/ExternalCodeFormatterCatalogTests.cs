using MarkLeaf.Services.CodeFormatting;

namespace MarkLeaf.Tests;

[TestClass]
public sealed class ExternalCodeFormatterCatalogTests
{
    [TestMethod]
    public void ToolForLanguage_MapsCanonicalAndAliasLanguages()
    {
        Assert.AreEqual("black", ExternalCodeFormatterCatalog.ToolForLanguage("python")?.Id);
        Assert.AreEqual("black", ExternalCodeFormatterCatalog.ToolForLanguage("  PY ")?.Id);
        Assert.AreEqual("google-java-format", ExternalCodeFormatterCatalog.ToolForLanguage("java")?.Id);
        Assert.AreEqual("verible-verilog-format", ExternalCodeFormatterCatalog.ToolForLanguage("systemverilog")?.Id);
        Assert.IsNull(ExternalCodeFormatterCatalog.ToolForLanguage("brainfuck"));
    }

    [TestMethod]
    public void ProbeAvailability_UsesConfiguredCustomPath()
    {
        var tool = ExternalCodeFormatterCatalog.ToolForLanguage("java")!;
        var customPath = Path.Combine(Path.GetTempPath(), $"markleaf-test-{Guid.NewGuid():N}.cmd");
        File.WriteAllText(customPath, "@echo off\r\n");
        try
        {
            var paths = new Dictionary<string, string> { [tool.Id] = customPath };

            Assert.AreEqual(
                ExternalCodeFormatterAvailability.Available,
                ExternalCodeFormatterCatalog.ProbeAvailability(tool, paths));
            Assert.AreEqual(customPath, ExternalCodeFormatterCatalog.ResolveExecutable(tool, paths));

            var missing = new Dictionary<string, string> { [tool.Id] = @"C:\definitely\missing.exe" };

            Assert.AreEqual(
                ExternalCodeFormatterAvailability.InvalidCustomPath,
                ExternalCodeFormatterCatalog.ProbeAvailability(tool, missing));
            Assert.IsNull(ExternalCodeFormatterCatalog.ResolveExecutable(tool, missing));
        }
        finally
        {
            File.Delete(customPath);
        }
    }

    [TestMethod]
    public void AvailableLanguages_UnionsLanguagesOfResolvedTools()
    {
        var tool = ExternalCodeFormatterCatalog.ToolForLanguage("java")!;
        var customPath = Path.Combine(Path.GetTempPath(), $"markleaf-test-{Guid.NewGuid():N}.cmd");
        File.WriteAllText(customPath, "@echo off\r\n");
        try
        {
            // 只提供一个工具的自定义路径（CI 环境不假设 PATH 上有任何格式化器），
            // java 语言必须出现，未安装工具的语言不得虚报。
            var paths = new Dictionary<string, string> { [tool.Id] = customPath };
            var languages = ExternalCodeFormatterCatalog.AvailableLanguages(paths);

            CollectionAssert.Contains(languages.ToList(), "java");
        }
        finally
        {
            File.Delete(customPath);
        }
    }

    [TestMethod]
    public void GoFmt_UsesHelpFlagForProbing()
    {
        var gofmt = ExternalCodeFormatterCatalog.ToolForLanguage("go")!;

        CollectionAssert.AreEqual(new[] { "-h" }, gofmt.ProbeArguments.ToList());
    }
}

[TestClass]
public sealed class ExternalCodeFormatterServiceTests
{
    [TestMethod]
    public void FirstLine_ReturnsTextBeforeFirstBreak()
    {
        Assert.AreEqual("can't locate File/HomeDir.pm", ExternalCodeFormatterService.FirstLine("can't locate File/HomeDir.pm in @INC\r\nsecond line\n"));
        Assert.AreEqual("plain", ExternalCodeFormatterService.FirstLine("plain"));
        Assert.AreEqual("", ExternalCodeFormatterService.FirstLine("  \r\n"));
    }
}
