using System;

namespace GlassBar
{
    /// <summary>Versteckt die Windows-Taskleiste und stellt sie beim Beenden wieder her.</summary>
    internal static class NativeTaskbar
    {
        static int? _origState;
        static bool _hidden;

        static Native.APPBARDATA Abd(IntPtr tray)
        {
            return new Native.APPBARDATA { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Native.APPBARDATA>(), hWnd = tray };
        }

        static void Each(Action<IntPtr> a)
        {
            var main = Native.FindWindow("Shell_TrayWnd", null);
            if (main != IntPtr.Zero) a(main);
            IntPtr sec = IntPtr.Zero;
            while ((sec = Native.FindWindowEx(IntPtr.Zero, sec, "Shell_SecondaryTrayWnd", null)) != IntPtr.Zero) a(sec);
        }

        public static void Hide()
        {
            var tray = Native.FindWindow("Shell_TrayWnd", null);
            if (tray == IntPtr.Zero) return;
            if (!_hidden)
            {
                var abd = Abd(tray);
                _origState = (int)Native.SHAppBarMessage(Native.ABM_GETSTATE, ref abd).ToInt64();
                abd = Abd(tray);
                abd.lParam = (IntPtr)(_origState.Value | Native.ABS_AUTOHIDE);
                Native.SHAppBarMessage(Native.ABM_SETSTATE, ref abd);
                _hidden = true;
            }
            Each(h => Native.ShowWindow(h, Native.SW_HIDE));
        }

        public static void Restore()
        {
            var tray = Native.FindWindow("Shell_TrayWnd", null);
            if (tray != IntPtr.Zero)
            {
                var abd = Abd(tray);
                int state = _origState ?? Native.ABS_ALWAYSONTOP;
                state &= ~Native.ABS_AUTOHIDE;
                abd.lParam = (IntPtr)(state | Native.ABS_ALWAYSONTOP);
                Native.SHAppBarMessage(Native.ABM_SETSTATE, ref abd);
            }
            Each(h => Native.ShowWindow(h, Native.SW_SHOW));
            _hidden = false;
        }

        public static bool IsHidden => _hidden;
    }
}
