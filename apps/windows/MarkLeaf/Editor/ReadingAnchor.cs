using System.Text.Json;

namespace MarkLeaf.Editor;

public enum ReadingAnchorKind
{
    Visual,
    Source,
}

/// <summary>
/// 与 packages/editor-core/src/reading-anchor.ts 对齐的稳定阅读锚点：
/// ordinal 是视口顶部可见的顶层块序号，total 是顶层块总数，token 是该块的
/// 文本指纹（NFKC + 空白折叠），fraction 是块内滚动比例 0-1。
/// </summary>
public sealed record ReadingAnchor(
    ReadingAnchorKind Kind,
    int Ordinal,
    int Total,
    string Token,
    double Fraction)
{
    public static ReadingAnchor? FromJson(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty("kind", out var kind)
            || kind.ValueKind != JsonValueKind.String
            // 前端契约固定发小写 visual|source；忽略大小写以兼容手写载荷。
            || !Enum.TryParse(kind.GetString(), ignoreCase: true, out ReadingAnchorKind anchorKind)
            || !element.TryGetProperty("ordinal", out var ordinal) || !ordinal.TryGetInt32(out var ordinalValue)
            || ordinalValue < 0
            || !element.TryGetProperty("total", out var total) || !total.TryGetInt32(out var totalValue)
            || totalValue < 1
            || !element.TryGetProperty("token", out var token)
            || token.ValueKind != JsonValueKind.String
            || token.GetString() is not { } tokenValue)
        {
            return null;
        }

        var fraction = 0d;
        if (element.TryGetProperty("fraction", out var fractionElement)
            && fractionElement.ValueKind == JsonValueKind.Number
            && fractionElement.TryGetDouble(out var parsedFraction)
            && double.IsFinite(parsedFraction))
        {
            fraction = Math.Clamp(parsedFraction, 0d, 1d);
        }

        return new ReadingAnchor(
            anchorKind,
            Math.Clamp(ordinalValue, 0, Math.Max(0, totalValue - 1)),
            totalValue,
            tokenValue,
            fraction);
    }

    public object ToPayload() => new
    {
        kind = Kind switch
        {
            ReadingAnchorKind.Source => "source",
            _ => "visual",
        },
        ordinal = Ordinal,
        total = Total,
        token = Token,
        fraction = Fraction,
    };
}
