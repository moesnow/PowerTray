using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PowerTray.Native;
using PowerTray.Services;
using PowerTray.Theme;
using PowerTray.Utils;
using Orientation = System.Windows.Controls.Orientation;

namespace PowerTray.Views;

/// <summary>设置窗口：自定义时间挡位、开机自启动。所有修改即时保存。</summary>
public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();

        SourceInitialized += (_, _) => ApplyTitleBarTheme();
        ThemeManager.ThemeChanged += OnThemeChanged;
        Closed += (_, _) => ThemeManager.ThemeChanged -= OnThemeChanged;
        Loaded += OnLoaded;
    }

    private void OnThemeChanged() =>
        Dispatcher.InvokeAsync(ApplyTitleBarTheme);

    private void ApplyTitleBarTheme() =>
        TaskbarHelper.ApplyDarkTitleBar(this, !ThemeManager.IsAppLight);

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        MaxHeight = SystemParameters.WorkArea.Height - 60;

        PowerManager.RefreshCapabilities();
        DeviceHint.Text = PowerManager.HasBattery
            ? "此设备带电池：弹窗中可切换「接通电源 / 使用电池」，挡位分别写入对应设置。"
            : "此设备为台式机：弹窗中显示一组挡位，应用时同时更新接通电源与使用电池的值。";

        SleepHint.Visibility = PowerManager.SleepSupported ? Visibility.Collapsed : Visibility.Visible;
        HibernateHint.Visibility = PowerManager.HibernateSupported ? Visibility.Collapsed : Visibility.Visible;

        StartWithWindowsCheck.IsChecked = AutoStartService.IsEnabled;
        VersionText.Text = "PowerTray " + (Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0");

        RebuildAll();
    }

    // ---- 挡位编辑 --------------------------------------------------------

    private List<int> ListFor(PowerKind kind) => kind switch
    {
        PowerKind.Sleep => SettingsStore.Current.SleepPresets,
        PowerKind.Hibernate => SettingsStore.Current.HibernatePresets,
        _ => SettingsStore.Current.ScreenOffPresets,
    };

    private void RebuildAll()
    {
        RebuildChips(ScreenChipsPanel, PowerKind.ScreenOff);
        RebuildChips(SleepChipsPanel, PowerKind.Sleep);
        RebuildChips(HibernateChipsPanel, PowerKind.Hibernate);
    }

    private void RebuildChips(WrapPanel panel, PowerKind kind)
    {
        panel.Children.Clear();

        foreach (int seconds in ListFor(kind).OrderBy(v => v))
        {
            var content = new StackPanel { Orientation = Orientation.Horizontal };
            content.Children.Add(new TextBlock { Text = TimeFormat.Label(seconds) });
            content.Children.Add(new TextBlock
            {
                Text = "✕",
                Margin = new Thickness(8, 0, 0, 0),
                FontSize = 10,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (Brush)FindResource("TextTertiaryBrush"),
            });

            var chip = new Button
            {
                Style = (Style)FindResource("RemovableChip"),
                Content = content,
                Margin = new Thickness(0, 0, 6, 6),
                ToolTip = "点击删除该挡位",
            };
            int captured = seconds;
            chip.Click += (_, _) => RemovePreset(kind, captured);
            panel.Children.Add(chip);
        }

        panel.Children.Add(new Button
        {
            Style = (Style)FindResource("FixedChip"),
            Content = "从不",
            Margin = new Thickness(0, 0, 6, 6),
            ToolTip = "固定选项：不关闭 / 不睡眠 / 不休眠",
        });
    }

    private void RemovePreset(PowerKind kind, int seconds)
    {
        ListFor(kind).Remove(seconds);
        SettingsStore.Save();
        RebuildAll();
    }

    private void AddPreset(PowerKind kind, TextBox input, RadioButton minute, TextBlock error)
    {
        void Fail(string message)
        {
            error.Text = message;
            error.Visibility = Visibility.Visible;
        }
        error.Visibility = Visibility.Collapsed;

        var list = ListFor(kind);
        if (!int.TryParse(input.Text.Trim(), out int amount) || amount <= 0)
        {
            Fail("请输入正整数。");
            return;
        }

        int seconds = (minute.IsChecked == true ? 60 : 3600) * amount;
        if (seconds is < AppDefaults.MinSeconds or > AppDefaults.MaxSeconds)
        {
            Fail("有效范围：1 分钟 ~ 24 小时。");
            return;
        }
        if (list.Contains(seconds))
        {
            Fail("该挡位已存在。");
            return;
        }
        if (list.Count >= AppDefaults.MaxPresetCount)
        {
            Fail($"最多 {AppDefaults.MaxPresetCount} 个挡位。");
            return;
        }

        list.Add(seconds);
        SettingsStore.Save();
        RebuildAll();
        input.Clear();
    }

    private void OnAddScreen(object sender, RoutedEventArgs e) =>
        AddPreset(PowerKind.ScreenOff, ScreenAddValue, ScreenUnitMinute, ScreenAddError);

    private void OnAddSleep(object sender, RoutedEventArgs e) =>
        AddPreset(PowerKind.Sleep, SleepAddValue, SleepUnitMinute, SleepAddError);

    private void OnAddHibernate(object sender, RoutedEventArgs e) =>
        AddPreset(PowerKind.Hibernate, HibernateAddValue, HibernateUnitMinute, HibernateAddError);

    private void OnResetPresets(object sender, RoutedEventArgs e)
    {
        SettingsStore.ResetPresets();
        RebuildAll();
    }

    // ---- 常规 ------------------------------------------------------------

    private void OnStartWithWindowsChanged(object sender, RoutedEventArgs e)
    {
        AutoStartService.SetEnabled(StartWithWindowsCheck.IsChecked == true);
    }

    private void OnDone(object sender, RoutedEventArgs e) => Close();

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Close();
    }
}
