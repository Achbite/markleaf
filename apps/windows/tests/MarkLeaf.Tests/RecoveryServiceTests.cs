using MarkLeaf.Services.Recovery;

namespace MarkLeaf.Tests;

/// <summary>
/// 1.7.7 恢复窗口“打开”动作依赖的清理契约：DeletePending 必须能删除崩溃进程
/// （其它 PID 前缀）留下的快照文件，且不影响其它文档的快照。
/// </summary>
[TestClass]
public sealed class RecoveryServiceTests
{
    [TestMethod]
    public void DeletePending_RemovesCrossProcessSnapshotFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "markleaf-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var documentId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var dataFile = Path.Combine(root, $"doc-9999-{documentId:N}.md");
        var metaFile = Path.Combine(root, $"doc-9999-{documentId:N}.md.meta");
        var tempFile = Path.Combine(root, $"doc-9999-{documentId:N}.md.{Guid.NewGuid():N}.tmp");
        var otherFile = Path.Combine(root, $"doc-1234-{otherId:N}.md");
        File.WriteAllText(dataFile, "# recovered");
        File.WriteAllText(metaFile, "{}");
        File.WriteAllText(tempFile, "partial");
        File.WriteAllText(otherFile, "# other");

        try
        {
            RecoveryService.DeletePending(root, documentId, new TestLogger());

            Assert.IsFalse(File.Exists(dataFile));
            Assert.IsFalse(File.Exists(metaFile));
            Assert.IsFalse(File.Exists(tempFile));
            Assert.IsTrue(File.Exists(otherFile));
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }

    [TestMethod]
    public void DeletePending_MissingDirectory_DoesNotThrow()
    {
        var root = Path.Combine(Path.GetTempPath(), "markleaf-tests", Guid.NewGuid().ToString("N"));

        RecoveryService.DeletePending(root, Guid.NewGuid(), new TestLogger());

        Assert.IsFalse(Directory.Exists(root));
    }
}
