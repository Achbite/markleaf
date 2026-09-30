using MarkLeaf.Editor;
using System.Text.Json;

namespace MarkLeaf.Tests;

/// <summary>
/// 1.7.7 同步新增的宿主协议契约：restoreViewport / markSaved / refreshOutline /
/// setCodeFormatterSettings / codeFormatResult 出站白名单，codeFormatRequested
/// 与 snapshot.readingAnchor 入站载荷校验，以及 ReadingAnchor 的 JSON 双向解析。
/// </summary>
[TestClass]
public sealed class EditorProtocolSyncTests
{
    [TestMethod]
    public void SerializeHostMessage_AcceptsNewSyncMessageTypes()
    {
        var documentId = Guid.NewGuid();
        foreach (var type in new[]
                 {
                     "restoreViewport",
                     "markSaved",
                     "refreshOutline",
                     "setCodeFormatterSettings",
                     "codeFormatResult",
                 })
        {
            var json = EditorProtocol.SerializeHostMessage(type, documentId, 3, new { });
            StringAssert.Contains(json, $@"""type"":""{type}""");
        }
    }

    [TestMethod]
    public void SerializeHostMessage_RestoreViewport_PayloadCarriesReadingAnchor()
    {
        var json = EditorProtocol.SerializeHostMessage(
            "restoreViewport",
            Guid.NewGuid(),
            1,
            new
            {
                scrollTop = (double?)null,
                selection = (object?)null,
                readingAnchor = new ReadingAnchor(ReadingAnchorKind.Visual, 2, 5, "target", 0.25).ToPayload(),
            });

        StringAssert.Contains(json, """readingAnchor""");
        StringAssert.Contains(json, """kind":"visual""");
        StringAssert.Contains(json, """token":"target""");
    }

    [TestMethod]
    public void TryDeserializeEditorMessage_AcceptsCodeFormatRequestWithSelectionLines()
    {
        var json = $$"""
            {
              "protocolVersion": 1,
              "type": "codeFormatRequested",
              "requestId": "format-1",
              "documentId": "{{Guid.NewGuid()}}",
              "revision": 7,
              "payload": { "code": "int a;", "language": "java", "startLine": 2, "endLine": 4 }
            }
            """;

        Assert.IsTrue(EditorProtocol.TryDeserializeEditorMessage(json, out var message, out var error), error);
        Assert.AreEqual("codeFormatRequested", message!.Type);
        Assert.AreEqual("format-1", message.RequestId);
    }

    [TestMethod]
    public void TryDeserializeEditorMessage_RejectsCodeFormatRequestWithInvalidLines()
    {
        var json = $$"""
            {
              "protocolVersion": 1,
              "type": "codeFormatRequested",
              "requestId": "format-1",
              "documentId": "{{Guid.NewGuid()}}",
              "revision": 7,
              "payload": { "code": "int a;", "language": "java", "startLine": 0 }
            }
            """;

        Assert.IsFalse(EditorProtocol.TryDeserializeEditorMessage(json, out _, out var error));
        Assert.AreEqual("Message payload is invalid.", error);
    }

    [TestMethod]
    public void TryDeserializeEditorMessage_AcceptsSnapshotWithReadingAnchor()
    {
        var json = $$"""
            {
              "protocolVersion": 1,
              "type": "snapshot",
              "requestId": "snapshot-1",
              "documentId": "{{Guid.NewGuid()}}",
              "revision": 2,
              "payload": {
                "markdown": "# Hello",
                "scrollTop": 120,
                "readingAnchor": { "kind": "source", "ordinal": 3, "total": 9, "token": "intro", "fraction": 0.5 }
              }
            }
            """;

        Assert.IsTrue(EditorProtocol.TryDeserializeEditorMessage(json, out var message, out var error), error);
        Assert.AreEqual("snapshot", message!.Type);
    }

    [TestMethod]
    public void TryDeserializeEditorMessage_RejectsSnapshotWithInvalidReadingAnchor()
    {
        var json = $$"""
            {
              "protocolVersion": 1,
              "type": "snapshot",
              "requestId": "snapshot-1",
              "documentId": "{{Guid.NewGuid()}}",
              "revision": 2,
              "payload": {
                "markdown": "# Hello",
                "readingAnchor": { "kind": "sideways", "ordinal": 3, "total": 9, "token": "intro" }
              }
            }
            """;

        Assert.IsFalse(EditorProtocol.TryDeserializeEditorMessage(json, out _, out var error));
        Assert.AreEqual("Message payload is invalid.", error);
    }

    [TestMethod]
    public void ReadingAnchor_FromJson_ParsesAndClamps()
    {
        var element = JsonDocument.Parse(
            """{ "kind": "visual", "ordinal": 12, "total": 5, "token": "段落", "fraction": 1.5 }""").RootElement;

        var anchor = ReadingAnchor.FromJson(element);

        Assert.IsNotNull(anchor);
        Assert.AreEqual(ReadingAnchorKind.Visual, anchor.Kind);
        // ordinal 夹取到 total-1，fraction 夹取到 [0,1]，与前端归一化契约一致。
        Assert.AreEqual(4, anchor.Ordinal);
        Assert.AreEqual(5, anchor.Total);
        Assert.AreEqual(1d, anchor.Fraction);
    }

    [TestMethod]
    public void ReadingAnchor_FromJson_ReturnsNullForMissingFields()
    {
        var element = JsonDocument.Parse("""{ "kind": "visual", "ordinal": 1 }""").RootElement;

        Assert.IsNull(ReadingAnchor.FromJson(element));
    }

    [TestMethod]
    public void ReadingAnchor_ToPayload_RoundTripsThroughFromJson()
    {
        var anchor = new ReadingAnchor(ReadingAnchorKind.Source, 1, 3, "block", 0.75);

        var payload = anchor.ToPayload();
        var json = JsonSerializer.Serialize(payload);
        var restored = ReadingAnchor.FromJson(JsonDocument.Parse(json).RootElement);

        Assert.IsNotNull(restored);
        Assert.AreEqual(anchor, restored);
    }
}
