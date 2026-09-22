using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PowerTray.Services;

public class AppConfig
{
    /// <summary>「关闭屏幕」挡位（秒）。</summary>
    public List<int> ScreenOffPresets { get; set; } = new(AppDefaults.ScreenOff);

    /// <summary>「睡眠」挡位（秒）。</summary>
    public List<int> SleepPresets { get; set; } = new(AppDefaults.Sleep);

    /// <summary>「休眠」挡位（秒）。</summary>
    public List<int> HibernatePresets { get; set; } = new(AppDefaults.Hibernate);

    public bool StartWithWindows { get; set; }
}

public static class AppDefaults
{
    public static readonly int[] ScreenOff = { 60, 180, 300, 600, 900, 1800, 3600 };
    public static readonly int[] Sleep = { 300, 900, 1800, 3600, 7200, 14400 };
    public static readonly int[] Hibernate = { 1800, 3600, 7200, 14400, 28800 };

    public const int MinSeconds = 60;        // 1 分钟
    public const int MaxSeconds = 86400;     // 24 小时
    public const int MaxPresetCount = 24;
}

public static class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public static AppConfig Current { get; private set; } = new();

    public static string FilePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PowerTray", "settings.json");

    public static void Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var config = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(FilePath), JsonOptions);
                if (config is not null) Current = Normalize(config);
            }
        }
        catch
        {
            Current = new AppConfig();
        }
    }

    public static void Save()
    {
        try
        {
            string dir = Path.GetDirectoryName(FilePath)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(Current, JsonOptions));
        }
        catch
        {
            // 配置写入失败不影响主功能。
        }
    }

    public static void ResetPresets()
    {
        Current.ScreenOffPresets = new List<int>(AppDefaults.ScreenOff);
        Current.SleepPresets = new List<int>(AppDefaults.Sleep);
        Current.HibernatePresets = new List<int>(AppDefaults.Hibernate);
        Save();
    }

    private static AppConfig Normalize(AppConfig config)
    {
        config.ScreenOffPresets = Clean(config.ScreenOffPresets, AppDefaults.ScreenOff);
        config.SleepPresets = Clean(config.SleepPresets, AppDefaults.Sleep);
        config.HibernatePresets = Clean(config.HibernatePresets, AppDefaults.Hibernate);
        return config;
    }

    private static List<int> Clean(List<int>? source, int[] fallback)
    {
        if (source is null || source.Count == 0) return new List<int>(fallback);
        return source
            .Where(v => v >= AppDefaults.MinSeconds && v <= AppDefaults.MaxSeconds)
            .Distinct()
            .OrderBy(v => v)
            .Take(AppDefaults.MaxPresetCount)
            .ToList();
    }
}
