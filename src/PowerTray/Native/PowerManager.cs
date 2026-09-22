using System.Runtime.InteropServices;

namespace PowerTray.Native;

public enum PowerKind
{
    ScreenOff,
    Sleep,
    Hibernate,
}

public enum PowerTarget
{
    Ac,
    Dc,
    Both,
}

/// <summary>
/// 读写「关闭屏幕 / 睡眠 / 休眠」超时（仅当前活动电源计划），
/// 并探测设备能力（是否支持睡眠、是否启用休眠、是否为笔记本）。
/// </summary>
public static class PowerManager
{
    private static readonly Guid VideoSubgroup = new("7516b95f-f776-4464-8c53-06167f40cc99");
    private static readonly Guid SleepSubgroup = new("238c9fa8-0aad-41ed-83f4-97be242c8f20");
    private static readonly Guid VideoIdle = new("3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e");
    private static readonly Guid StandbyIdle = new("29f6c1db-86da-48c5-9fdb-f2b67b1f44da");
    private static readonly Guid HibernateIdle = new("9d7815a6-7ee4-497e-8888-515a05f02364");

    /// <summary>设备支持进入睡眠（S1-S3 或现代待机）。</summary>
    public static bool SleepSupported { get; private set; }

    /// <summary>设备已启用休眠（hiberfil.sys 存在）。</summary>
    public static bool HibernateSupported { get; private set; }

    /// <summary>设备带电池（笔记本 / 平板）。</summary>
    public static bool HasBattery { get; private set; }

    /// <summary>当前是否接通电源。</summary>
    public static bool IsOnAc { get; private set; } = true;

    /// <summary>台式机只暴露一组挡位，写入时同时写 AC/DC 两套值。</summary>
    public static PowerTarget DesktopTarget => PowerTarget.Both;

    public static void RefreshCapabilities()
    {
        IntPtr buffer = Marshal.AllocHGlobal(512);
        try
        {
            for (int i = 0; i < 512; i++) Marshal.WriteByte(buffer, i, 0);
            if (NativeMethods.GetPwrCapabilities(buffer))
            {
                bool s1 = Marshal.ReadByte(buffer, NativeMethods.CapSystemS1) != 0;
                bool s2 = Marshal.ReadByte(buffer, NativeMethods.CapSystemS2) != 0;
                bool s3 = Marshal.ReadByte(buffer, NativeMethods.CapSystemS3) != 0;
                bool aoAc = Marshal.ReadByte(buffer, NativeMethods.CapAoAc) != 0;
                bool hiber = Marshal.ReadByte(buffer, NativeMethods.CapHiberFilePresent) != 0;

                SleepSupported = s1 || s2 || s3 || aoAc;
                HibernateSupported = hiber;
            }
            else
            {
                // 探测失败时保守处理：全部显示。
                SleepSupported = true;
                HibernateSupported = true;
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }

        var status = new SYSTEM_POWER_STATUS();
        if (GetSystemPowerStatus(ref status))
        {
            HasBattery = (status.BatteryFlag & 128) == 0;
            IsOnAc = status.ACLineStatus == 1;
        }
        else
        {
            HasBattery = false;
            IsOnAc = true;
        }
    }

    /// <summary>读取超时（秒）。0 = 从不；-1 = 读取失败。</summary>
    public static int GetTimeout(PowerKind kind, bool ac)
    {
        IntPtr scheme = GetActiveScheme();
        if (scheme == IntPtr.Zero) return -1;
        try
        {
            (Guid subgroup, Guid setting) = Map(kind);
            int hr = ac
                ? NativeMethods.PowerReadACValueIndex(IntPtr.Zero, scheme, ref subgroup, ref setting, out int value)
                : NativeMethods.PowerReadDCValueIndex(IntPtr.Zero, scheme, ref subgroup, ref setting, out value);
            return hr == 0 ? value : -1;
        }
        finally
        {
            FreeActiveScheme(scheme);
        }
    }

    /// <summary>写入超时（秒，0 = 从不），并立即对当前计划生效。</summary>
    public static bool SetTimeout(PowerKind kind, int seconds, PowerTarget target)
    {
        if (seconds < 0) return false;
        IntPtr scheme = GetActiveScheme();
        if (scheme == IntPtr.Zero) return false;
        try
        {
            (Guid subgroup, Guid setting) = Map(kind);
            bool ok = true;
            if (target is PowerTarget.Ac or PowerTarget.Both)
            {
                ok &= NativeMethods.PowerWriteACValueIndex(IntPtr.Zero, scheme, ref subgroup, ref setting, seconds) == 0;
            }
            if (target is PowerTarget.Dc or PowerTarget.Both)
            {
                ok &= NativeMethods.PowerWriteDCValueIndex(IntPtr.Zero, scheme, ref subgroup, ref setting, seconds) == 0;
            }

            // 重新激活当前计划，让系统立即应用新值（与 powercfg /change 行为一致）。
            NativeMethods.PowerSetActiveScheme(IntPtr.Zero, scheme);
            return ok;
        }
        finally
        {
            FreeActiveScheme(scheme);
        }
    }

    private static (Guid Subgroup, Guid Setting) Map(PowerKind kind) => kind switch
    {
        PowerKind.ScreenOff => (VideoSubgroup, VideoIdle),
        PowerKind.Sleep => (SleepSubgroup, StandbyIdle),
        PowerKind.Hibernate => (SleepSubgroup, HibernateIdle),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static IntPtr GetActiveScheme()
    {
        return NativeMethods.PowerGetActiveScheme(IntPtr.Zero, out IntPtr guid) == 0 ? guid : IntPtr.Zero;
    }

    private static void FreeActiveScheme(IntPtr scheme)
    {
        if (scheme != IntPtr.Zero) NativeMethods.LocalFree(scheme);
    }

    // ---- battery / AC state ---------------------------------------------
    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEM_POWER_STATUS
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public int BatteryLifeTime;
        public int BatteryFullLifeTime;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemPowerStatus(ref SYSTEM_POWER_STATUS sps);
}
