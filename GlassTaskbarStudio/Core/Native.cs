using System;
using System.Runtime.InteropServices;
using System.Text;

namespace GlassTaskbarStudio.Core
{
    /// <summary>All Win32 interop used by the app in one place.</summary>
    internal static class Native
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left, Top, Right, Bottom;
            public int Width => Right - Left;
            public int Height => Bottom - Top;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MONITORINFO { public int cbSize; public RECT rcMonitor; public RECT rcWork; public uint dwFlags; }

        [StructLayout(LayoutKind.Sequential)]
        struct AccentPolicy { public int AccentState; public int AccentFlags; public uint GradientColor; public int AnimationId; }

        [StructLayout(LayoutKind.Sequential)]
        struct WindowCompositionAttributeData { public int Attribute; public IntPtr Data; public int SizeOfData; }

        public const int RGN_OR = 2;
        public const int GWL_EXSTYLE = -20;
        public const long WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x08000000, WS_EX_TRANSPARENT = 0x20;
        public const uint SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2, SWP_NOACTIVATE = 0x10, SWP_SHOWWINDOW = 0x40, SWP_HIDEWINDOW = 0x80;
        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        public static readonly IntPtr HWND_BROADCAST = new IntPtr(0xFFFF);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindow(string cls, string title);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, string title);
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] public static extern int SetWindowRgn(IntPtr h, IntPtr rgn, bool redraw);
        [DllImport("user32.dll")] public static extern int GetWindowRgn(IntPtr h, IntPtr rgn);
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] public static extern IntPtr GetWindowLongPtr(IntPtr h, int idx);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] public static extern IntPtr SetWindowLongPtr(IntPtr h, int idx, IntPtr v);
        [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr h);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder sb, int max);
        [DllImport("user32.dll")] public static extern IntPtr MonitorFromWindow(IntPtr h, uint flags);
        [DllImport("user32.dll")] public static extern bool GetMonitorInfo(IntPtr mon, ref MONITORINFO mi);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern uint RegisterWindowMessage(string s);
        [DllImport("user32.dll")] static extern int SetWindowCompositionAttribute(IntPtr h, ref WindowCompositionAttributeData d);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "SendMessageTimeoutW")]
        public static extern IntPtr SendMessageTimeout(IntPtr h, uint msg, UIntPtr w, string l, uint flags, uint timeout, out UIntPtr result);

        [DllImport("gdi32.dll")] public static extern IntPtr CreateRoundRectRgn(int l, int t, int r, int b, int w, int h);
        [DllImport("gdi32.dll")] public static extern IntPtr CreateRectRgn(int l, int t, int r, int b);
        [DllImport("gdi32.dll")] public static extern int CombineRgn(IntPtr dest, IntPtr a, IntPtr b, int mode);
        [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr o);

        [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int value, int size);

        /// <summary>
        /// Sets the (undocumented but long-stable) accent policy of a window.
        /// state: 0 = disabled, 1 = opaque gradient, 2 = transparent gradient (clear), 3 = blur behind, 4 = acrylic.
        /// </summary>
        public static void SetAccent(IntPtr hwnd, int state, byte r, byte g, byte b, byte alpha)
        {
            var accent = new AccentPolicy
            {
                AccentState = state,
                AccentFlags = 2,
                GradientColor = ((uint)alpha << 24) | ((uint)b << 16) | ((uint)g << 8) | r
            };
            int size = Marshal.SizeOf<AccentPolicy>();
            IntPtr ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(accent, ptr, false);
                var data = new WindowCompositionAttributeData { Attribute = 19, Data = ptr, SizeOfData = size };
                SetWindowCompositionAttribute(hwnd, ref data);
            }
            finally { Marshal.FreeHGlobal(ptr); }
        }

        public static string GetClass(IntPtr h)
        {
            var sb = new StringBuilder(256);
            GetClassName(h, sb, sb.Capacity);
            return sb.ToString();
        }

        public static void MakeClickThroughOverlay(IntPtr hwnd)
        {
            long ex = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64() | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TRANSPARENT;
            SetWindowLongPtr(hwnd, GWL_EXSTYLE, (IntPtr)ex);
        }
    }
}
