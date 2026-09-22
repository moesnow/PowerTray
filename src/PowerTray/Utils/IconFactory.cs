using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace PowerTray.Utils;

/// <summary>
/// 绘制与 Windows 11 托盘风格一致的单色电源符号图标（自动适配浅色 / 深色任务栏）。
/// </summary>
public static class IconFactory
{
    public static Icon CreateTrayIcon(bool systemLight)
    {
        const int size = 32;
        using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            var color = systemLight ? Color.FromArgb(58, 58, 58) : Color.FromArgb(242, 242, 242);
            DrawPowerGlyph(g, size, color);
        }

        IntPtr hIcon = bitmap.GetHicon();
        try
        {
            using var temp = Icon.FromHandle(hIcon);
            return (Icon)temp.Clone();
        }
        finally
        {
            Native.NativeMethods.DestroyIcon(hIcon);
        }
    }

    /// <summary>电源符号：带顶部缺口的圆环 + 竖线。几何与 tools/Generate-Icon.ps1 保持一致。</summary>
    public static void DrawPowerGlyph(Graphics g, int size, Color color)
    {
        float penWidth = size * 0.085f;
        using var pen = new Pen(color, penWidth)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
        };

        float margin = size * 0.17f;
        var ring = new RectangleF(margin, margin, size - 2 * margin, size - 2 * margin);
        g.DrawArc(pen, ring, -55, 290);

        float centerX = size / 2f;
        g.DrawLine(pen, centerX, size * 0.11f, centerX, size * 0.50f);
    }
}
