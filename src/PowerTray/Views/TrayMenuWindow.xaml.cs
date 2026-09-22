using System.Windows;
using System.Windows.Input;
using PowerTray.Utils;

namespace PowerTray.Views;

/// <summary>
/// 托盘右键菜单（轻量窗口实现）。
/// 与快速弹窗一致：点击菜单外空白处 / 菜单内空白处 / 按 Esc / 失焦即自动关闭。
/// </summary>
public partial class TrayMenuWindow : Window
{
    private readonly Action _openSettings;
    private readonly Action _exitApp;

    public TrayMenuWindow(Action openSettings, Action exitApp)
    {
        InitializeComponent();
        _openSettings = openSettings;
        _exitApp = exitApp;
        Loaded += OnLoaded;
        // 任何关闭路径（Close / Shutdown）都先标记，避免关闭过程中 Deactivated 再次 Close 抛异常。
        Closing += (_, _) => _closing = true;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateLayout();
        TaskbarHelper.PlaceAtCursor(this);
    }

    private bool _closing;

    /// <summary>关闭菜单（防止关闭过程中 Deactivated 等事件再次触发 Close 而抛异常）。</summary>
    private void CloseFlyout()
    {
        if (_closing) return;
        _closing = true;
        Close();
    }

    private void OnSettings(object sender, RoutedEventArgs e)
    {
        CloseFlyout();
        _openSettings();
    }

    private void OnExit(object sender, RoutedEventArgs e)
    {
        CloseFlyout();
        _exitApp();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) CloseFlyout();
    }

    private void OnDeactivated(object? sender, EventArgs e) => CloseFlyout();

    private void OnBlankAreaMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (FlyoutHelper.IsInteractive(e.OriginalSource as DependencyObject)) return;
        CloseFlyout();
    }
}
