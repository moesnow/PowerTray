using System.Windows;
using System.Windows.Media;

namespace PowerTray.Utils;

/// <summary>轻量浮层（快速弹窗 / 托盘菜单）通用行为：判断点击是否落在可交互控件上。</summary>
public static class FlyoutHelper
{
    /// <summary>点击源（或其父级）是否为按钮、输入框、滚动条等可交互元素。</summary>
    public static bool IsInteractive(DependencyObject? source)
    {
        while (source is not null)
        {
            switch (source)
            {
                case System.Windows.Controls.Primitives.ButtonBase:
                case System.Windows.Controls.Primitives.TextBoxBase:
                case System.Windows.Controls.Primitives.ScrollBar:
                case System.Windows.Controls.Primitives.Thumb:
                    return true;
                case FrameworkElement { Tag: "KeepOpen" }:
                    return true;
            }
            source = GetParent(source);
        }
        return false;
    }

    public static DependencyObject? GetParent(DependencyObject node)
    {
        if (node is Visual or System.Windows.Media.Media3D.Visual3D)
        {
            var visualParent = VisualTreeHelper.GetParent(node);
            if (visualParent is not null) return visualParent;
        }
        return LogicalTreeHelper.GetParent(node);
    }
}
