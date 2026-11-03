using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace GlassBar
{
    /// <summary>Glas-Popup mit Live-Vorschau (DWM-Thumbnail) eines Fensters.</summary>
    public partial class PreviewWindow : Window
    {
        IntPtr _hwnd, _thumb;

        public PreviewWindow() { InitializeComponent(); }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            _hwnd = new WindowInteropHelper(this).Handle;
            HwndSource.FromHwnd(_hwnd).CompositionTarget.BackgroundColor = Colors.Transparent;
            Native.MakeToolWindow(_hwnd, true, true); // nicht aktivierbar, Maus geht durch
            Native.EnableAcrylic(_hwnd, 20, 28, 50, 120);
        }

        void ClearThumb()
        {
            if (_thumb != IntPtr.Zero) { Native.DwmUnregisterThumbnail(_thumb); _thumb = IntPtr.Zero; }
        }

        public void HidePreview()
        {
            ClearThumb();
            if (IsVisible) Hide();
        }

        public void ShowFor(IntPtr source, string title, double anchorXpx, double barEdgePx, bool barOnTop)
        {
            ClearThumb();
            if (_hwnd == IntPtr.Zero) { Show(); Hide(); } // Handle sicherstellen
            double dpi = VisualTreeHelper.GetDpi(this).DpiScaleX;

            if (Native.DwmRegisterThumbnail(_hwnd, source, out _thumb) != 0) { _thumb = IntPtr.Zero; HidePreview(); return; }
            if (Native.DwmQueryThumbnailSourceSize(_thumb, out var sz) != 0 || sz.cx <= 0 || sz.cy <= 0) { HidePreview(); return; }

            double scale = Math.Min(1.0, Math.Min(280.0 / (sz.cx / dpi), 170.0 / (sz.cy / dpi)));
            double tw = sz.cx / dpi * scale, th = sz.cy / dpi * scale;
            double winW = Math.Max(tw + 24, 180), winH = th + 50;

            TitleText.Text = title;
            Width = winW; Height = winH;
            int wpx = (int)(winW * dpi), hpx = (int)(winH * dpi);
            int screenW = Native.GetSystemMetrics(0);
            int x = (int)(anchorXpx - wpx / 2.0);
            x = Math.Max(8, Math.Min(screenW - wpx - 8, x));
            int gap = (int)(10 * dpi);
            int y = barOnTop ? (int)barEdgePx + gap : (int)barEdgePx - hpx - gap;

            Native.SetWindowPos(_hwnd, Native.HWND_TOPMOST, x, y, wpx, hpx, Native.SWP_NOACTIVATE | Native.SWP_SHOWWINDOW);
            Native.RoundCorners(_hwnd, wpx, hpx, (int)(18 * dpi));

            int left = (int)((winW - tw) / 2 * dpi), top = (int)(34 * dpi);
            var props = new Native.DWM_THUMBNAIL_PROPERTIES
            {
                dwFlags = 0x1 | 0x4 | 0x8, // Ziel-Rechteck | Deckkraft | sichtbar
                rcDestination = new Native.RECT { Left = left, Top = top, Right = left + (int)(tw * dpi), Bottom = top + (int)(th * dpi) },
                opacity = 255,
                fVisible = 1
            };
            Native.DwmUpdateThumbnailProperties(_thumb, ref props);
        }

        protected override void OnClosed(EventArgs e) { ClearThumb(); base.OnClosed(e); }
    }
}
