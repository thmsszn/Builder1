using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace GlassBar
{
    internal static class IconHelper
    {
        public static BitmapSource FromHIcon(IntPtr hIcon)
        {
            try
            {
                var bmp = Imaging.CreateBitmapSourceFromHIcon(hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                bmp.Freeze();
                return bmp;
            }
            catch { return null; }
        }

        public static BitmapSource FromPath(string path)
        {
            try
            {
                using (var icon = System.Drawing.Icon.ExtractAssociatedIcon(path))
                    return icon == null ? null : FromHIcon(icon.Handle);
            }
            catch { return null; }
        }

        public static BitmapSource ForWindow(IntPtr hwnd)
        {
            IntPtr h;
            Native.SendMessageTimeout(hwnd, Native.WM_GETICON, (IntPtr)2, IntPtr.Zero, 2, 100, out h);
            if (h == IntPtr.Zero) Native.SendMessageTimeout(hwnd, Native.WM_GETICON, (IntPtr)1, IntPtr.Zero, 2, 100, out h);
            if (h == IntPtr.Zero) h = Native.GetClassLongPtr(hwnd, -34); // GCL_HICONSM
            if (h == IntPtr.Zero) h = Native.GetClassLongPtr(hwnd, -14); // GCL_HICON
            var src = h != IntPtr.Zero ? FromHIcon(h) : null;
            if (src != null) return src;
            try
            {
                Native.GetWindowThreadProcessId(hwnd, out uint pid);
                using (var p = Process.GetProcessById((int)pid))
                    return FromPath(p.MainModule.FileName);
            }
            catch { return null; }
        }
    }
}
