using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using GlassTaskbarStudio.UI.Controls;

namespace GlassTaskbarStudio.Core
{
    /// <summary>
    /// A click-through, never-activating layered window sitting exactly over a taskbar. Windows cannot draw gloss,
    /// rim light or shadows on its own taskbar, so these purely cosmetic effects live here. Mouse input passes through.
    /// </summary>
    internal sealed class OverlayWindow : Window
    {
        readonly GlassPillVisual _visual = new GlassPillVisual();
        readonly Grid _root = new Grid();
        IntPtr _hwnd;
        string _lastKey;

        public OverlayWindow()
        {
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            ShowActivated = false;
            Topmost = true;
            ResizeMode = ResizeMode.NoResize;
            IsHitTestVisible = false;
            Focusable = false;
            Width = 10; Height = 10; Left = -20000; Top = -20000; WindowStartupLocation = WindowStartupLocation.Manual;
            _root.IsHitTestVisible = false;
            _root.Children.Add(_visual);
            Content = _root;
            SourceInitialized += (s, e) =>
            {
                _hwnd = new WindowInteropHelper(this).Handle;
                Native.MakeClickThroughOverlay(_hwnd);
            };
        }

        /// <summary>Pad around the taskbar so shadows and glows have room.</summary>
        public const double PadDip = 30;

        public void UpdateOverlay(Native.RECT bar, PillLayout layout, TaskbarSettings s, double dpiScale)
        {
            if (_hwnd == IntPtr.Zero) { Show(); if (_hwnd == IntPtr.Zero) return; }
            int pad = (int)Math.Round(PadDip * dpiScale);
            int x = bar.Left - pad, y = bar.Top - pad, w = bar.Width + 2 * pad, h = bar.Height + 2 * pad;

            Native.SetWindowPos(_hwnd, Native.HWND_TOPMOST, x, y, w, h, Native.SWP_NOACTIVATE | Native.SWP_SHOWWINDOW);

            // 1 canvas unit == 1 device pixel; scale the whole thing back to DIPs for WPF.
            double wpfScale = VisualTreeHelper.GetDpi(this).DpiScaleX;
            _visual.RenderTransform = new ScaleTransform(1.0 / wpfScale, 1.0 / wpfScale);

            string key = $"{x},{y},{w},{h}|{layout.Key}|{wpfScale:0.##}|{dpiScale:0.##}|" +
                         $"{s.GlossIntensity:0.#}{s.ShadowIntensity:0.#}{s.BorderOpacity:0.#}{s.BorderThickness:0.#}{s.ShadowHex}{s.BorderHex}{s.Shimmer}";
            if (key == _lastKey) return;
            _lastKey = key;

            var pills = new List<Rect>();
            foreach (var p in layout.Pills) pills.Add(new Rect(p.X + pad, p.Y + pad, p.Width, p.Height));
            _visual.Render(new Size(w, h), pills, layout.Radius, s, dpiScale, null, Rect.Empty, animate: true);
        }

        public void HideOverlay()
        {
            if (_hwnd != IntPtr.Zero) Native.SetWindowPos(_hwnd, IntPtr.Zero, 0, 0, 0, 0,
                Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE | Native.SWP_HIDEWINDOW);
            _lastKey = null;
        }
    }
}
