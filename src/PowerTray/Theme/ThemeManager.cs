using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace PowerTray.Theme;

/// <summary>
/// 跟随系统的浅色 / 深色模式与系统强调色；模式变化时热切换资源字典。
/// </summary>
public static class ThemeManager
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string DwmKey = @"Software\Microsoft\Windows\DWM";

    private const int WmSettingChange = 0x001A;
    private const int WmDwmColorizationColorChanged = 0x0320;

    private static readonly Color DefaultLightAccent = Color.FromRgb(0x00, 0x67, 0xC0);
    private static readonly Color DefaultDarkAccent = Color.FromRgb(0x4C, 0xC2, 0xFF);

    private static HwndSource? _broadcastWindow;
    private static DispatcherTimer? _debounce;

    /// <summary>应用界面是否使用浅色（AppsUseLightTheme）。</summary>
    public static bool IsAppLight { get; private set; } = true;

    /// <summary>任务栏 / 系统是否使用浅色（SystemUsesLightTheme），决定托盘图标颜色。</summary>
    public static bool IsSystemLight { get; private set; } = true;

    /// <summary>主题或强调色发生变化后触发。</summary>
    public static event Action? ThemeChanged;

    /// <summary>自检用：当前主题与强调色的解析详情。</summary>
    public static string DebugInfo { get; private set; } = "";

    private static string _accentDebug = "";

    public static void Initialize()
    {
        IsAppLight = ReadAppLight();
        IsSystemLight = ReadSystemLight();
        Apply();
        StartWatcher();
    }

    public static void Apply()
    {
        bool appLight = ReadAppLight();
        bool systemLight = ReadSystemLight();
        bool changed = appLight != IsAppLight || systemLight != IsSystemLight;
        IsAppLight = appLight;
        IsSystemLight = systemLight;

        var app = Application.Current;
        if (app is not null)
        {
            var resources = app.Resources;
            var dictionaries = resources.MergedDictionaries;

            var theme = dictionaries.FirstOrDefault(d =>
                d.Source is not null &&
                (d.Source.OriginalString.EndsWith("Light.xaml", StringComparison.OrdinalIgnoreCase) ||
                 d.Source.OriginalString.EndsWith("Dark.xaml", StringComparison.OrdinalIgnoreCase)));

            var fresh = LoadThemeDictionary(appLight);
            ApplyAccent(fresh, appLight);
            if (theme is not null)
            {
                int index = dictionaries.IndexOf(theme);
                dictionaries[index] = fresh;
            }
            else
            {
                dictionaries.Insert(0, fresh);
            }
        }

        if (changed) ThemeChanged?.Invoke();

        DebugInfo = $"theme={(appLight ? "light" : "dark")} system={(systemLight ? "light" : "dark")} {_accentDebug}";
    }

    private static bool ReadAppLight() =>
        ForcedTheme() ?? ReadDword(PersonalizeKey, "AppsUseLightTheme", 1) != 0;

    private static bool ReadSystemLight() =>
        ForcedTheme() ?? ReadDword(PersonalizeKey, "SystemUsesLightTheme", 1) != 0;

    /// <summary>测试用：环境变量 POWERTRAY_THEME=light|dark 可强制主题（不影响系统设置）。</summary>
    private static bool? ForcedTheme()
    {
        string? forced = Environment.GetEnvironmentVariable("POWERTRAY_THEME");
        return string.Equals(forced, "light", StringComparison.OrdinalIgnoreCase) ? true
             : string.Equals(forced, "dark", StringComparison.OrdinalIgnoreCase) ? false
             : null;
    }

    private static ResourceDictionary LoadThemeDictionary(bool appLight)
    {
        string file = appLight ? "Theme/Light.xaml" : "Theme/Dark.xaml";
        return new ResourceDictionary { Source = new Uri(file, UriKind.Relative) };
    }

    // ---- 强调色 -----------------------------------------------------------

    private static void ApplyAccent(ResourceDictionary theme, bool appLight)
    {
        Color accent = GetAccentColor(appLight);
        Color onAccent = GetReadableForeground(accent);

        SetBrush(theme, "AccentBrush", accent);
        SetBrush(theme, "ChipCheckedBackgroundBrush", accent);
        SetBrush(theme, "AccentHoverBrush", Blend(accent, Colors.White, 0.14));
        SetBrush(theme, "AccentPressedBrush", Blend(accent, Colors.Black, 0.14));
        SetBrush(theme, "AccentTextBrush", onAccent);
        SetBrush(theme, "ChipCheckedTextBrush", onAccent);
    }

    private static Color GetAccentColor(bool appLight)
    {
        // 1) 优先使用注册表里的用户强调色（ABGR，不透明）——与 Windows 设置 App 一致。
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(DwmKey);
            if (key?.GetValue("AccentColor") is int value && value != 0)
            {
                var color = FromColorRef(unchecked((uint)value));
                _accentDebug = $"accent=reg:0x{value:X8}->#{color.R:X2}{color.G:X2}{color.B:X2}";
                return color;
            }
        }
        catch
        {
            // 忽略，回退到 DWM 颜色化颜色。
        }

        // 2) DWM 颜色化颜色（0xAARRGGBB，通常是强调色的半透明混合，可能偏暗）。
        try
        {
            if (Native.NativeMethods.DwmGetColorizationColor(out uint argb, out _) == 0)
            {
                var color = Color.FromArgb(
                    0xFF,
                    (byte)((argb >> 16) & 0xFF),
                    (byte)((argb >> 8) & 0xFF),
                    (byte)(argb & 0xFF));
                _accentDebug = $"accent=dwm:0x{argb:X8}->#{color.R:X2}{color.G:X2}{color.B:X2}";
                if (Saturation(color) > 0.06) return color;
            }
        }
        catch
        {
            // 忽略，回退到默认值。
        }

        // 3) 内置默认强调色。
        _accentDebug = $"accent=default({(appLight ? "light" : "dark")})";
        return appLight ? DefaultLightAccent : DefaultDarkAccent;
    }

    private static int ReadDword(string keyPath, string valueName, int fallback)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(keyPath);
            return key?.GetValue(valueName) is int value ? value : fallback;
        }
        catch
        {
            return fallback;
        }
    }

    private static Color FromColorRef(uint value) =>
        Color.FromArgb(0xFF, (byte)(value & 0xFF), (byte)((value >> 8) & 0xFF), (byte)((value >> 16) & 0xFF));

    private static double Luminance(Color c) =>
        (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0;

    private static double Saturation(Color c)
    {
        double max = Math.Max(c.R, Math.Max(c.G, c.B));
        double min = Math.Min(c.R, Math.Min(c.G, c.B));
        return max <= 0 ? 0 : (max - min) / max;
    }

    private static Color GetReadableForeground(Color background) =>
        Luminance(background) > 0.55 ? Colors.Black : Colors.White;

    private static Color Blend(Color baseColor, Color overlay, double amount)
    {
        byte Mix(byte a, byte b) => (byte)(a + (b - a) * amount);
        return Color.FromRgb(
            Mix(baseColor.R, overlay.R),
            Mix(baseColor.G, overlay.G),
            Mix(baseColor.B, overlay.B));
    }

    private static void SetBrush(ResourceDictionary dictionary, string key, Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        dictionary[key] = brush;
    }

    // ---- 变更监听 ---------------------------------------------------------

    private static void StartWatcher()
    {
        _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _debounce.Tick += (_, _) =>
        {
            _debounce!.Stop();
            Apply();
        };

        // 真正的顶层隐藏窗口（message-only 窗口收不到广播消息）。
        var parameters = new HwndSourceParameters("PowerTrayThemeWatcher")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0,
        };
        _broadcastWindow = new HwndSource(parameters);
        _broadcastWindow.AddHook(WndProc);

        SystemEvents.UserPreferenceChanged += (_, args) =>
        {
            if (args.Category is UserPreferenceCategory.General or UserPreferenceCategory.Color)
                ScheduleApply();
        };
    }

    private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg is WmSettingChange or WmDwmColorizationColorChanged) ScheduleApply();
        return IntPtr.Zero;
    }

    private static void ScheduleApply()
    {
        if (_debounce is null) return;
        _debounce.Stop();
        _debounce.Start();
    }
}
