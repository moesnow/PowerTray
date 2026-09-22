using System.Runtime.InteropServices;

namespace PowerTray.Native;

internal static class NativeMethods
{
    // ---- powrprof -------------------------------------------------------
    [DllImport("powrprof.dll", SetLastError = true)]
    internal static extern int PowerGetActiveScheme(IntPtr userRootPowerKey, out IntPtr activePolicyGuid);

    [DllImport("powrprof.dll", SetLastError = true)]
    internal static extern int PowerReadACValueIndex(
        IntPtr rootPowerKey, IntPtr schemeGuid, ref Guid subGroupOfPowerSettingsGuid,
        ref Guid powerSettingGuid, out int valueIndex);

    [DllImport("powrprof.dll", SetLastError = true)]
    internal static extern int PowerReadDCValueIndex(
        IntPtr rootPowerKey, IntPtr schemeGuid, ref Guid subGroupOfPowerSettingsGuid,
        ref Guid powerSettingGuid, out int valueIndex);

    [DllImport("powrprof.dll", SetLastError = true)]
    internal static extern int PowerWriteACValueIndex(
        IntPtr rootPowerKey, IntPtr schemeGuid, ref Guid subGroupOfPowerSettingsGuid,
        ref Guid powerSettingGuid, int valueIndex);

    [DllImport("powrprof.dll", SetLastError = true)]
    internal static extern int PowerWriteDCValueIndex(
        IntPtr rootPowerKey, IntPtr schemeGuid, ref Guid subGroupOfPowerSettingsGuid,
        ref Guid powerSettingGuid, int valueIndex);

    [DllImport("powrprof.dll", SetLastError = true)]
    internal static extern int PowerSetActiveScheme(IntPtr userRootPowerKey, IntPtr schemeGuid);

    [DllImport("powrprof.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool GetPwrCapabilities(IntPtr systemPowerCapabilities);

    // ---- kernel32 -------------------------------------------------------
    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern IntPtr LocalFree(IntPtr hMem);

    // ---- user32 ---------------------------------------------------------
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool DestroyIcon(IntPtr hIcon);

    // ---- dwmapi ---------------------------------------------------------
    [DllImport("dwmapi.dll", PreserveSig = true)]
    internal static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    /// <summary>返回值颜色格式为 0xAARRGGBB（注意与 COLORREF 的 0x00BBGGRR 不同）。</summary>
    [DllImport("dwmapi.dll", PreserveSig = true)]
    internal static extern int DwmGetColorizationColor(
        out uint pcrColorization,
        [MarshalAs(UnmanagedType.Bool)] out bool pfOpaqueBlend);

    [StructLayout(LayoutKind.Sequential)]
    internal struct POINT
    {
        public int X;
        public int Y;
    }

    // SYSTEM_POWER_CAPABILITIES offsets used by PowerManager (winnt.h layout).
    internal const int CapSystemS1 = 3;
    internal const int CapSystemS2 = 4;
    internal const int CapSystemS3 = 5;
    internal const int CapHiberFilePresent = 8;
    internal const int CapAoAc = 19;

    // DWM window attributes
    internal const int DwmwaUseImmersiveDarkMode = 20;
    internal const int DwmwaWindowCornerPreference = 33;
}
