using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PowerTray.Native;
using PowerTray.Services;
using PowerTray.Theme;
using PowerTray.Utils;
using PowerTray.Views;
using Application = System.Windows.Application;
using NotifyIcon = System.Windows.Forms.NotifyIcon;

namespace PowerTray;

public partial class App : Application
{
    private const string MutexName = "PowerTray_SingleInstance_{2E7A61D4-6B8C-4E5E-9F36-7A0C4E2B98D1}";

    private Mutex? _mutex;
    private NotifyIcon? _tray;
    private System.Drawing.Icon? _trayIcon;
    private TrayMenuWindow? _menu;
    private QuickPopup? _popup;
    private SettingsWindow? _settings;
    private DateTime _lastPopupClosedUtc = DateTime.MinValue;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _mutex = new Mutex(true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            Shutdown();
            return;
        }

        DispatcherUnhandledException += (_, args) =>
        {
            LogError(args.Exception);
            args.Handled = true;
        };

        SettingsStore.Load();
        PowerManager.RefreshCapabilities();
        ThemeManager.Initialize();
        ThemeManager.ThemeChanged += OnThemeChanged;

        BuildTrayIcon();

        if (e.Args.Any(a => string.Equals(a, "--settings", StringComparison.OrdinalIgnoreCase)))
        {
            ShowSettings();
        }

        if (e.Args.Any(a => string.Equals(a, "--selftest", StringComparison.OrdinalIgnoreCase)))
        {
            RunSelfTest();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        ThemeManager.ThemeChanged -= OnThemeChanged;
        if (_tray is not null)
        {
            _tray.Visible = false;
            _tray.Dispose();
            _tray = null;
        }
        _trayIcon?.Dispose();
        _trayIcon = null;
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }

    // ---- 托盘 ------------------------------------------------------------

    private void BuildTrayIcon()
    {
        _trayIcon = IconFactory.CreateTrayIcon(ThemeManager.IsSystemLight);
        _tray = new NotifyIcon
        {
            Icon = _trayIcon,
            Text = "PowerTray — 左键快速设置关闭屏幕 / 睡眠 / 休眠时间",
            Visible = true,
        };
        _tray.MouseClick += OnTrayMouseClick;
    }

    private void OnTrayMouseClick(object? sender, System.Windows.Forms.MouseEventArgs e)
    {
        switch (e.Button)
        {
            case System.Windows.Forms.MouseButtons.Left:
                TogglePopup();
                break;
            case System.Windows.Forms.MouseButtons.Right:
                ShowMenu();
                break;
        }
    }

    private void OnThemeChanged()
    {
        var previous = _trayIcon;
        _trayIcon = IconFactory.CreateTrayIcon(ThemeManager.IsSystemLight);
        if (_tray is not null) _tray.Icon = _trayIcon;
        previous?.Dispose();
    }

    // ---- 右键菜单 --------------------------------------------------------

    private void ShowMenu()
    {
        _menu?.Close();
        var menu = new TrayMenuWindow(ShowSettings, ExitApp);
        _menu = menu;
        menu.Closed += (_, _) =>
        {
            if (ReferenceEquals(_menu, menu)) _menu = null;
        };
        menu.Show();
        menu.Activate();
    }

    // ---- 快速弹窗 --------------------------------------------------------

    private void TogglePopup()
    {
        if (_popup is not null)
        {
            _popup.Close();
            return;
        }

        // 弹窗失焦时会自动关闭；若刚刚关闭（例如再次点击托盘图标），
        // 则视为“收起”，避免立刻重新弹出。
        if ((DateTime.UtcNow - _lastPopupClosedUtc).TotalMilliseconds < 400)
        {
            _lastPopupClosedUtc = DateTime.MinValue;
            return;
        }

        var popup = new QuickPopup();
        _popup = popup;
        popup.Closed += (_, _) =>
        {
            if (ReferenceEquals(_popup, popup)) _popup = null;
            _lastPopupClosedUtc = DateTime.UtcNow;
        };
        popup.Show();
        popup.Activate();
    }

    // ---- 设置窗口 --------------------------------------------------------

    private void ShowSettings()
    {
        if (_settings is not null)
        {
            _settings.Activate();
            return;
        }

        var window = new SettingsWindow();
        _settings = window;
        window.Closed += (_, _) =>
        {
            if (ReferenceEquals(_settings, window)) _settings = null;
        };
        window.Show();
        window.Activate();
    }

    private void ExitApp()
    {
        _menu?.Close();
        _popup?.Close();
        _settings?.Close();
        Shutdown();
    }

    // ---- 自检（--selftest）：验证三个界面可构建加载，并输出截图与诊断报告 --------

    internal static string SelfTestReportPath =>
        Path.Combine(Path.GetTempPath(), "powertray-selftest.txt");

    private async void RunSelfTest()
    {
        var report = new StringBuilder();

        // 1. 快速弹窗
        bool popupLoaded = false, popupClosed = false;
        var popup = new QuickPopup();
        popup.Loaded += (_, _) => popupLoaded = true;
        popup.Closed += (_, _) => popupClosed = true;
        popup.Show();
        await Task.Delay(900);
        Capture(popup, "powertray-popup.png");
        report.AppendLine($"popupLoaded={popupLoaded}");

        // 2. 设置窗口（弹窗应因失焦自动关闭）
        bool settingsLoaded = false;
        var settings = new SettingsWindow();
        settings.Loaded += (_, _) => settingsLoaded = true;
        settings.Show();
        await Task.Delay(900);
        Capture(settings, "powertray-settings.png");
        report.AppendLine($"settingsLoaded={settingsLoaded} popupClosedByDeactivate={popupClosed}");
        popup.Close();

        // 3. 托盘右键菜单
        var menu = new TrayMenuWindow(ShowSettings, ExitApp);
        _menu = menu;
        menu.Show();
        await Task.Delay(600);
        Capture(menu, "powertray-menu.png");
        report.AppendLine($"menuLoaded={menu.IsLoaded} menuShot=powertray-menu.png");
        menu.Close();
        _menu = null;
        settings.Close();

        PowerManager.RefreshCapabilities();
        report.AppendLine($"sleepSupported={PowerManager.SleepSupported} hibernateSupported={PowerManager.HibernateSupported} hasBattery={PowerManager.HasBattery} isOnAc={PowerManager.IsOnAc}");
        report.AppendLine($"screenOff(ac)={PowerManager.GetTimeout(PowerKind.ScreenOff, true)} sleep(ac)={PowerManager.GetTimeout(PowerKind.Sleep, true)} hibernate(ac)={PowerManager.GetTimeout(PowerKind.Hibernate, true)}");
        report.AppendLine($"startWithWindows={AutoStartService.IsEnabled}");
        report.AppendLine(ThemeManager.DebugInfo);
        report.AppendLine($"shots={Path.Combine(Path.GetTempPath(), "powertray-popup.png")},{Path.Combine(Path.GetTempPath(), "powertray-settings.png")},{Path.Combine(Path.GetTempPath(), "powertray-menu.png")}");

        File.WriteAllText(SelfTestReportPath, report.ToString());

        Shutdown();

        // 看门狗：确保自检进程一定能退出（正常情况下 Shutdown 已足够）。
        _ = Task.Run(async () =>
        {
            await Task.Delay(4000);
            Environment.Exit(0);
        });
    }

    private static void Capture(Window window, string fileName)
    {
        try
        {
            window.UpdateLayout();
            int width = Math.Max(1, (int)Math.Ceiling(window.ActualWidth));
            int height = Math.Max(1, (int)Math.Ceiling(window.ActualHeight));
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(window);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(Path.Combine(Path.GetTempPath(), fileName));
            encoder.Save(stream);
        }
        catch (Exception ex)
        {
            LogError(ex);
        }
    }

    private static void LogError(Exception exception)
    {
        try
        {
            string dir = Path.GetDirectoryName(SettingsStore.FilePath)!;
            Directory.CreateDirectory(dir);
            File.AppendAllText(
                Path.Combine(dir, "error.log"),
                $"[{DateTime.Now:O}] {exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // 日志失败不影响运行。
        }
    }
}
