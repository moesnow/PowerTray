using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using PowerTray.Native;
using PowerTray.Services;
using PowerTray.Utils;

namespace PowerTray.Views;

/// <summary>托盘左键弹出的快速设置面板：点击挡位立即生效，点击空白处 / Esc / 失焦关闭。</summary>
public partial class QuickPopup : Window
{
    private sealed class Section
    {
        public PowerKind Kind;
        public TextBlock CurrentLabel = null!;
        public readonly List<(ToggleButton Chip, int Seconds)> Chips = new();
    }

    private readonly List<Section> _sections = new();
    private bool _isLaptop;
    private bool _onAc = true;

    public QuickPopup()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        // 任何关闭路径（Close / Shutdown）都先标记，避免关闭过程中 Deactivated 再次 Close 抛异常。
        Closing += (_, _) => _closing = true;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Rebuild();
        UpdateLayout();
        TaskbarHelper.PlaceNearTray(this);
    }

    // ---- 构建 ----------------------------------------------------------

    private void Rebuild()
    {
        SectionsHost.Children.Clear();
        _sections.Clear();

        PowerManager.RefreshCapabilities();
        _isLaptop = PowerManager.HasBattery;
        _onAc = _isLaptop ? PowerManager.IsOnAc : true;

        if (_isLaptop)
        {
            SourceSelectorPanel.Visibility = Visibility.Visible;
            if (_onAc) AcRadio.IsChecked = true;
            else DcRadio.IsChecked = true;
        }
        else
        {
            SourceSelectorPanel.Visibility = Visibility.Collapsed;
        }

        AddSection(PowerKind.ScreenOff, SettingsStore.Current.ScreenOffPresets);

        // 设备能力不支持的项不在弹窗中显示（设置页仍可编辑）。
        if (PowerManager.SleepSupported) AddSection(PowerKind.Sleep, SettingsStore.Current.SleepPresets);
        if (PowerManager.HibernateSupported) AddSection(PowerKind.Hibernate, SettingsStore.Current.HibernatePresets);

        RefreshValues();
    }

    private void AddSection(PowerKind kind, IEnumerable<int> presets)
    {
        var section = new Section { Kind = kind };

        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var title = new TextBlock
        {
            Text = HeaderFor(kind),
            Style = (Style)FindResource("SectionTitle"),
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(title, 0);

        var current = new TextBlock
        {
            Text = "当前 —",
            Style = (Style)FindResource("SecondaryText"),
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(current, 1);

        header.Children.Add(title);
        header.Children.Add(current);
        section.CurrentLabel = current;

        var wrap = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
        var values = presets.Where(v => v > 0).Distinct().OrderBy(v => v).Append(0);
        foreach (int seconds in values)
        {
            var chip = new ToggleButton
            {
                Style = (Style)FindResource("ChipToggle"),
                Content = TimeFormat.Label(seconds),
                Margin = new Thickness(0, 0, 6, 6),
                MinWidth = 56,
            };
            int captured = seconds;
            chip.Click += (_, _) => Apply(kind, captured);
            wrap.Children.Add(chip);
            section.Chips.Add((chip, captured));
        }

        var panel = new StackPanel { Margin = new Thickness(0, 14, 0, 0) };
        panel.Children.Add(header);
        panel.Children.Add(wrap);
        SectionsHost.Children.Add(panel);
        _sections.Add(section);
    }

    // ---- 交互 ----------------------------------------------------------

    private void Apply(PowerKind kind, int seconds)
    {
        var target = _isLaptop ? (_onAc ? PowerTarget.Ac : PowerTarget.Dc) : PowerTarget.Both;
        PowerManager.SetTimeout(kind, seconds, target);
        RefreshValues();
    }

    private void RefreshValues()
    {
        foreach (var section in _sections)
        {
            int value = PowerManager.GetTimeout(section.Kind, _onAc);
            section.CurrentLabel.Text = value < 0 ? "当前 —" : $"当前 {TimeFormat.Label(value)}";
            foreach (var (chip, seconds) in section.Chips)
            {
                chip.IsChecked = value >= 0 && seconds == value;
            }
        }
    }

    private void OnPowerSourceChanged(object sender, RoutedEventArgs e)
    {
        _onAc = AcRadio.IsChecked == true;
        if (_sections.Count > 0) RefreshValues();
    }

    private bool _closing;

    /// <summary>关闭弹窗（防止关闭过程中 Deactivated 等事件再次触发 Close 而抛异常）。</summary>
    private void CloseFlyout()
    {
        if (_closing) return;
        _closing = true;
        Close();
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

    private static string HeaderFor(PowerKind kind) => kind switch
    {
        PowerKind.ScreenOff => "关闭屏幕",
        PowerKind.Sleep => "睡眠",
        PowerKind.Hibernate => "休眠",
        _ => kind.ToString(),
    };
}
