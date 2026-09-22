namespace PowerTray.Utils;

public static class TimeFormat
{
    /// <summary>秒 → 展示文案（如 “5 分钟”“2 小时”“从不”）。</summary>
    public static string Label(int seconds) => seconds switch
    {
        <= 0 => "从不",
        _ when seconds % 3600 == 0 => $"{seconds / 3600} 小时",
        _ when seconds % 60 == 0 => $"{seconds / 60} 分钟",
        _ => $"{seconds} 秒",
    };
}
