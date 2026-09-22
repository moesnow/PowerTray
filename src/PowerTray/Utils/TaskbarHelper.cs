using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media;
using Point = System.Windows.Point;

namespace PowerTray.Utils;

public enum TaskbarEdge
{
    Bottom,
    Top,
    Left,
    Right,
}

/// <summary>定位快速弹窗到托盘图标附近（支持任意任务栏方位与 DPI 缩放）。</summary>
public static class TaskbarHelper
{
    private const int MarginPx = 8;
    private const int GapPx = 12;
    private const int AnchorOffsetPx = 48;

    public static void PlaceNearTray(Window window)
    {
        Native.NativeMethods.GetCursorPos(out var cursor);
        var screen = Screen.FromPoint(new System.Drawing.Point(cursor.X, cursor.Y));
        var bounds = screen.Bounds;
        var work = screen.WorkingArea;

        var edge = DetectEdge(bounds, work);

        var source = PresentationSource.FromVisual(window);
        var toDevice = source?.CompositionTarget?.TransformToDevice ?? Matrix.Identity;
        var toLogical = source?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;

        double widthPx = window.ActualWidth * toDevice.M11;
        double heightPx = window.ActualHeight * toDevice.M22;

        double leftPx;
        double topPx;
        switch (edge)
        {
            case TaskbarEdge.Bottom:
                leftPx = cursor.X - widthPx + AnchorOffsetPx;
                topPx = cursor.Y - heightPx - GapPx;
                break;
            case TaskbarEdge.Top:
                leftPx = cursor.X - widthPx + AnchorOffsetPx;
                topPx = cursor.Y + GapPx;
                break;
            case TaskbarEdge.Right:
                leftPx = cursor.X - widthPx - GapPx;
                topPx = cursor.Y - heightPx + AnchorOffsetPx;
                break;
            default:
                leftPx = cursor.X + GapPx;
                topPx = cursor.Y - heightPx + AnchorOffsetPx;
                break;
        }

        // 限制在工作区内，避免超出屏幕。
        double maxLeft = work.Right - widthPx - MarginPx;
        double maxTop = work.Bottom - heightPx - MarginPx;
        leftPx = Math.Clamp(leftPx, work.Left + MarginPx, Math.Max(work.Left + MarginPx, maxLeft));
        topPx = Math.Clamp(topPx, work.Top + MarginPx, Math.Max(work.Top + MarginPx, maxTop));

        var logical = toLogical.Transform(new Point(leftPx, topPx));
        window.Left = logical.X;
        window.Top = logical.Y;
    }

    /// <summary>把轻量浮层（托盘菜单）定位到光标附近，空间不足时自动翻转（与原生菜单行为一致）。</summary>
    public static void PlaceAtCursor(Window window)
    {
        Native.NativeMethods.GetCursorPos(out var cursor);
        var screen = Screen.FromPoint(new System.Drawing.Point(cursor.X, cursor.Y));
        var work = screen.WorkingArea;

        var source = PresentationSource.FromVisual(window);
        var toDevice = source?.CompositionTarget?.TransformToDevice ?? Matrix.Identity;
        var toLogical = source?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;

        double widthPx = window.ActualWidth * toDevice.M11;
        double heightPx = window.ActualHeight * toDevice.M22;

        const int offsetPx = 4;
        const int marginPx = 8;

        double leftPx = cursor.X + offsetPx;
        double topPx = cursor.Y + offsetPx;

        if (leftPx + widthPx > work.Right - marginPx) leftPx = cursor.X - widthPx - offsetPx;
        if (topPx + heightPx > work.Bottom - marginPx) topPx = cursor.Y - heightPx - offsetPx;

        leftPx = Math.Clamp(leftPx, work.Left + marginPx, Math.Max(work.Left + marginPx, work.Right - widthPx - marginPx));
        topPx = Math.Clamp(topPx, work.Top + marginPx, Math.Max(work.Top + marginPx, work.Bottom - heightPx - marginPx));

        var logical = toLogical.Transform(new Point(leftPx, topPx));
        window.Left = logical.X;
        window.Top = logical.Y;
    }

    private static TaskbarEdge DetectEdge(System.Drawing.Rectangle bounds, System.Drawing.Rectangle work)
    {
        int bottom = bounds.Bottom - work.Bottom;
        int top = work.Top - bounds.Top;
        int left = work.Left - bounds.Left;
        int right = bounds.Right - work.Right;

        int max = Math.Max(Math.Max(bottom, top), Math.Max(left, right));
        return max switch
        {
            _ when max == bottom => TaskbarEdge.Bottom,
            _ when max == top => TaskbarEdge.Top,
            _ when max == right => TaskbarEdge.Right,
            _ => TaskbarEdge.Left,
        };
    }

    /// <summary>设置原生标题栏的深色模式（Windows 11）。</summary>
    public static void ApplyDarkTitleBar(Window window, bool dark)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;
        int value = dark ? 1 : 0;
        Native.NativeMethods.DwmSetWindowAttribute(
            hwnd, Native.NativeMethods.DwmwaUseImmersiveDarkMode, ref value, sizeof(int));
    }
}
