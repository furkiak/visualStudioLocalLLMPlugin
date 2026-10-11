using System;
using System.Runtime.InteropServices;

namespace LLLMPluginModern.UI.Controls
{
    /// <summary>DWM interop for Windows 11 rounded corners, dark title bars and the acrylic backdrop.</summary>
    internal static class NativeMethods
    {
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_OLD = 19;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

        private const int DWMWCP_ROUND = 2;
        private const int DWMSBT_TRANSIENTWINDOW = 3; // Acrylic

        private static readonly Lazy<int> Build = new Lazy<int>(GetBuildNumber);

        /// <summary>Windows 11 22H2 (build 22621) introduced DWMWA_SYSTEMBACKDROP_TYPE.</summary>
        public static bool SupportsBackdrop => Build.Value >= 22621;

        public static bool SupportsRoundedCorners => Build.Value >= 22000;

        public static void SetDarkMode(IntPtr hwnd, bool dark)
        {
            if (Build.Value < 17763)
                return;
            int value = dark ? 1 : 0;
            int attribute = Build.Value >= 18985 ? DWMWA_USE_IMMERSIVE_DARK_MODE : DWMWA_USE_IMMERSIVE_DARK_MODE_OLD;
            TrySet(hwnd, attribute, value);
        }

        public static void SetRoundedCorners(IntPtr hwnd)
        {
            if (SupportsRoundedCorners)
                TrySet(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, DWMWCP_ROUND);
        }

        public static bool TrySetAcrylicBackdrop(IntPtr hwnd)
        {
            return SupportsBackdrop && TrySet(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, DWMSBT_TRANSIENTWINDOW);
        }

        private static bool TrySet(IntPtr hwnd, int attribute, int value)
        {
            try
            {
                return hwnd != IntPtr.Zero && DwmSetWindowAttribute(hwnd, attribute, ref value, sizeof(int)) == 0;
            }
            catch (Exception)
            {
                // dwmapi missing or unsupported attribute: keep the opaque look.
                return false;
            }
        }

        private static int GetBuildNumber()
        {
            try
            {
                var info = new OSVERSIONINFOEX { dwOSVersionInfoSize = Marshal.SizeOf(typeof(OSVERSIONINFOEX)) };
                return RtlGetVersion(ref info) == 0 ? info.dwBuildNumber : Environment.OSVersion.Version.Build;
            }
            catch (Exception)
            {
                return Environment.OSVersion.Version.Build;
            }
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        [DllImport("ntdll.dll")]
        private static extern int RtlGetVersion(ref OSVERSIONINFOEX versionInfo);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct OSVERSIONINFOEX
        {
            public int dwOSVersionInfoSize;
            public int dwMajorVersion;
            public int dwMinorVersion;
            public int dwBuildNumber;
            public int dwPlatformId;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szCSDVersion;

            public ushort wServicePackMajor;
            public ushort wServicePackMinor;
            public ushort wSuiteMask;
            public byte wProductType;
            public byte wReserved;
        }
    }
}
