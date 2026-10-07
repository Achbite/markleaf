using System.Drawing.Drawing2D;

namespace MarkLeaf.UI.Controls;

internal static class SidebarGdi
{
    public static GraphicsPath CreateRoundedRect(Rectangle bounds, int radius)
    {
        var d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    /// <summary>以抗锯齿填充圆角矩形，自动保存/恢复 SmoothingMode。</summary>
    public static void FillRoundedRect(Graphics g, Rectangle bounds, int radius, Brush brush)
    {
        using var path = CreateRoundedRect(bounds, radius);
        var old = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.FillPath(brush, path);
        g.SmoothingMode = old;
    }

    /// <summary>以抗锯齿描边圆角矩形，自动保存/恢复 SmoothingMode。</summary>
    public static void DrawRoundedRect(Graphics g, Rectangle bounds, int radius, Pen pen)
    {
        using var path = CreateRoundedRect(bounds, radius);
        var old = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.DrawPath(pen, path);
        g.SmoothingMode = old;
    }

    public static int ScaleForDpi(this Control control, int value)
        => (int)Math.Round(value * control.DeviceDpi / 96d);

    /// <summary>
    /// 按显式 DPI 把磅值字号换算为像素。GDI+ 的磅值字体按创建时 DC 的 DPI 换算，
    /// 部分宿主上下文（例如经 dotnet 宿主启动、进程处于 PerMonitor V1）中该值
    /// 不随显示器缩放更新，因此自绘控件统一按传入 DPI 以像素为单位重建字体。
    /// </summary>
    public static float FontPixels(float points, int dpi) => points * dpi / 72f;

    /// <summary>
    /// 按显式 DPI 缩放布局值。DPI 变更处理中必须传入事件给定的目标 DPI：
    /// Control.DeviceDpi 在 PerMonitor V1 上下文中会冻结在进程启动时的值。
    /// </summary>
    public static int ScaleForDpi(int value, int dpi) => (int)Math.Round(value * dpi / 96d);

    public static int ScaleGapForDpi(this Control control)
        => control.DeviceDpi switch
        {
            96 => 0,
            120 => 3,
            144 => 9,
            168 => 12,
            192 => 16,
            216 => 20,
            240 => 25,
            _ => control.ScaleForDpi(6),
        };
}
