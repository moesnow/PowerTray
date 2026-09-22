// PowerTray 电源读写功能自测：
// 1. 探测设备能力（睡眠 / 休眠 / 电池）；
// 2. 读取三类超时，写入 300 秒后读回验证，再还原；
// 3. 打印 powercfg /q 原始输出，用于人工核对 GUID 对应的设置项名称。
using System.Diagnostics;
using PowerTray.Native;

int failures = 0;

PowerManager.RefreshCapabilities();
Console.WriteLine($"capabilities: sleepSupported={PowerManager.SleepSupported} " +
                  $"hibernateSupported={PowerManager.HibernateSupported} " +
                  $"hasBattery={PowerManager.HasBattery} isOnAc={PowerManager.IsOnAc}");

foreach (var kind in new[] { PowerKind.ScreenOff, PowerKind.Sleep, PowerKind.Hibernate })
{
    int ac = PowerManager.GetTimeout(kind, true);
    int dc = PowerManager.GetTimeout(kind, false);
    Console.WriteLine($"read {kind}: ac={ac} dc={dc}");
}

// 写入 / 读回验证（屏幕超时 300 秒，两套值都写，最后还原）
int beforeAc = PowerManager.GetTimeout(PowerKind.ScreenOff, true);
int beforeDc = PowerManager.GetTimeout(PowerKind.ScreenOff, false);

bool ok = PowerManager.SetTimeout(PowerKind.ScreenOff, 300, PowerTarget.Both);
int readAc = PowerManager.GetTimeout(PowerKind.ScreenOff, true);
int readDc = PowerManager.GetTimeout(PowerKind.ScreenOff, false);
Console.WriteLine($"write screenOff=300(both): ok={ok} readback ac={readAc} dc={readDc}");

if (!ok || readAc != 300 || readDc != 300)
{
    failures++;
    Console.WriteLine("FAIL: 写入后未能读回 300 秒");
}
else
{
    Console.WriteLine("PASS: 写入后 AC/DC 均读回 300 秒");
}

// 还原
if (beforeAc >= 0) PowerManager.SetTimeout(PowerKind.ScreenOff, beforeAc, PowerTarget.Ac);
if (beforeDc >= 0) PowerManager.SetTimeout(PowerKind.ScreenOff, beforeDc, PowerTarget.Dc);
int restoredAc = PowerManager.GetTimeout(PowerKind.ScreenOff, true);
int restoredDc = PowerManager.GetTimeout(PowerKind.ScreenOff, false);
Console.WriteLine($"restored screenOff: ac={restoredAc} dc={restoredDc} (before ac={beforeAc} dc={beforeDc})");
if (restoredAc != beforeAc || restoredDc != beforeDc)
{
    failures++;
    Console.WriteLine("FAIL: 还原后的值与原始值不一致");
}

// powercfg 原始输出（人工核对 GUID 与设置项名称）
foreach (var query in new[]
{
    "SCHEME_CURRENT SUB_VIDEO VIDEOIDLE",
    "SCHEME_CURRENT SUB_SLEEP STANDBYIDLE",
    "SCHEME_CURRENT SUB_SLEEP HIBERNATEIDLE",
})
{
    Console.WriteLine();
    Console.WriteLine($"---- powercfg /q {query} ----");
    var psi = new ProcessStartInfo("powercfg.exe", $"/q {query}")
    {
        RedirectStandardOutput = true,
        UseShellExecute = false,
    };
    using var process = Process.Start(psi)!;
    Console.WriteLine(process.StandardOutput.ReadToEnd().TrimEnd());
    process.WaitForExit();
}

Console.WriteLine();
Console.WriteLine(failures == 0 ? "ALL PASS" : $"FAILURES: {failures}");
return failures == 0 ? 0 : 1;
