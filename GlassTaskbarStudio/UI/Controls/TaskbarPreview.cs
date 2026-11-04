using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using GlassTaskbarStudio.Core;
using Microsoft.Win32;

namespace GlassTaskbarStudio.UI.Controls
{
    /// <summary>
    /// Live preview of the taskbar. A virtual 1280 px wide desktop is scaled to the control width and the taskbar
    /// is laid out with the SAME LayoutCalculator and GlassPillVisual the real engine uses.
    /// The frosted backdrop is a simulation (clearly: the preview, not the desktop); everything else is geometry-identical.
    /// </summary>
    public sealed class TaskbarPreview : Border
    {
        const double VirtualW = 1280, VirtualH = 720;

        public static readonly DependencyProperty SettingsProperty = DependencyProperty.Register(nameof(Settings), typeof(TaskbarSettings),
            typeof(TaskbarPreview), new PropertyMetadata(null, (d, e) => ((TaskbarPreview)d).OnSettingsChanged((TaskbarSettings)e.OldValue, (TaskbarSettings)e.NewValue)));

        public TaskbarSettings Settings { get => (TaskbarSettings)GetValue(SettingsProperty); set => SetValue(SettingsProperty, value); }

        /// <summary>Compact variant for preset cards (smaller icons, no background switcher).</summary>
        public bool IsMini { get; set; }

        readonly Canvas _stage = new Canvas { ClipToBounds = true };
        readonly GlassPillVisual _glass = new GlassPillVisual();
        readonly Canvas _icons = new Canvas { IsHitTestVisible = false };
        readonly Button _bgButton = new Button { Content = "Change backdrop", Cursor = System.Windows.Input.Cursors.Hand };
        int _backdropMode; // 0 wallpaper, 1 aurora, 2 light
        ImageSource _backdropCache; string _backdropKey;
        bool _dirty;
        static ImageSource _wallpaper;
        static bool _wallpaperLoaded;

        public TaskbarPreview()
        {
            CornerRadius = new CornerRadius(18);
            ClipToBounds = true;
            BorderThickness = new Thickness(1);
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x30, 255, 255, 255));
            Child = _stage;
            _bgButton.Style = (Style)Application.Current.FindResource("GlassButton");
            _bgButton.Padding = new Thickness(12, 5, 12, 6);
            _bgButton.FontSize = 11.5;
            _bgButton.Click += (s, e) => { _backdropMode = (_backdropMode + 1) % 3; Rebuild(); };
            SizeChanged += (s, e) => Rebuild();
            Loaded += (s, e) => Rebuild();
        }

        void OnSettingsChanged(TaskbarSettings oldS, TaskbarSettings newS)
        {
            if (oldS != null) oldS.PropertyChanged -= SettingsChanged;
            if (newS != null) newS.PropertyChanged += SettingsChanged;
            Schedule();
        }

        void SettingsChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) => Schedule();

        void Schedule()
        {
            if (_dirty) return;
            _dirty = true;
            Dispatcher.BeginInvoke(new Action(Rebuild), System.Windows.Threading.DispatcherPriority.Render);
        }

        // ------------------------------------------------------------------

        void Rebuild()
        {
            _dirty = false;
            var s = Settings;
            double w = ActualWidth, h = ActualHeight;
            if (s == null || w < 40 || h < 40) return;

            double sc = w / VirtualW;                    // virtual px -> preview px
            double viewH = h / sc;                       // visible virtual height (bottom part of the desktop)
            _stage.Children.Clear();
            _stage.Width = w; _stage.Height = h;

            // desktop backdrop + a mock window so the blur has something to blur
            var rect = new Rect(0, h - VirtualH * sc, VirtualW * sc, VirtualH * sc);
            var key = $"{_backdropMode}|{(int)rect.Width}x{(int)rect.Height}";
            if (key != _backdropKey || _backdropCache == null) { _backdropCache = BuildBackdrop(rect); _backdropKey = key; }
            var backdrop = _backdropCache;
            Clip = new RectangleGeometry(new Rect(0, 0, w, h), CornerRadius.TopLeft, CornerRadius.TopLeft);
            var bdImage = new Image { Source = backdrop, Width = rect.Width, Height = rect.Height, Stretch = Stretch.Fill };
            Canvas.SetLeft(bdImage, rect.X); Canvas.SetTop(bdImage, rect.Y);
            _stage.Children.Add(bdImage);

            // taskbar geometry (virtual px -> scaled)
            double barH = s.Height == TaskbarSize.Small ? 40 : s.Height == TaskbarSize.Large ? 56 : 48;
            double barTopV = viewH - barH;
            double iconPitch = 44, iconCount = 6; // start + 5 apps
            double groupW = iconPitch * iconCount;
            double groupX = s.Alignment == IconAlignment.Center ? (VirtualW - groupW) / 2 : 12;
            double trayW = 190;

            var layout = LayoutCalculator.Compute(s, VirtualW * sc, barH * sc, sc, trayW * sc,
                                                  new Rect(groupX * sc, 0, groupW * sc, barH * sc), true);
            double barTop = barTopV * sc;
            var pills = new List<Rect>();
            foreach (var p in layout.Pills) pills.Add(new Rect(p.X + rect.X, p.Y + barTop, p.Width, p.Height));

            _glass.Render(new Size(w, h), pills, layout.Radius, s, Math.Max(sc, 0.35), backdrop, rect, true);
            _stage.Children.Add(_glass);

            BuildIcons(s, sc, barTop, barH * sc, groupX * sc + rect.X, iconPitch * sc, trayW * sc, w);
            _stage.Children.Add(_icons);

            if (!IsMini)
            {
                Canvas.SetRight(_bgButton, 12); Canvas.SetTop(_bgButton, 12);
                _stage.Children.Add(_bgButton);
                var tag = new TextBlock
                {
                    Text = "LIVE PREVIEW  -  simulated backdrop, real geometry", FontSize = 10, FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromArgb(0xB0, 255, 255, 255)), IsHitTestVisible = false
                };
                Canvas.SetLeft(tag, 16); Canvas.SetTop(tag, 16);
                _stage.Children.Add(tag);
            }
        }

        // ---------------- icons ----------------

        void BuildIcons(TaskbarSettings s, double sc, double barTop, double barH, double groupX, double pitch, double trayW, double totalW)
        {
            _icons.Children.Clear();
            double size = 22 * sc * (IsMini ? 1.15 : 1);
            double cy = barTop + barH / 2;
            string[] colors = { "#F5B942", "#4ADE80", "#60A5FA", "#F472B6", "#A78BFA" };

            // Start button (4 panes)
            double x = groupX + pitch / 2;
            double q = size / 2 - 0.6 * sc;
            for (int i = 0; i < 4; i++)
            {
                var r = new Rectangle { Width = q, Height = q, RadiusX = 1.2 * sc, RadiusY = 1.2 * sc, Fill = new SolidColorBrush(Color.FromRgb(0x6C, 0xB8, 0xFF)) };
                Canvas.SetLeft(r, x - size / 2 + (i % 2) * (q + 1.2 * sc)); Canvas.SetTop(r, cy - size / 2 + (i / 2) * (q + 1.2 * sc));
                _icons.Children.Add(r);
            }
            for (int i = 0; i < 5; i++)
            {
                double ix = groupX + pitch * (i + 1.5);
                var r = new Rectangle
                {
                    Width = size, Height = size, RadiusX = 6 * sc, RadiusY = 6 * sc,
                    Fill = new LinearGradientBrush(ColorUtil.Parse(colors[i]), ColorUtil.Lerp(ColorUtil.Parse(colors[i]), Colors.Black, 0.35), 90)
                };
                Canvas.SetLeft(r, ix - size / 2); Canvas.SetTop(r, cy - size / 2);
                _icons.Children.Add(r);
                if (i == 1) // running indicator
                {
                    var bar = new Rectangle { Width = 12 * sc, Height = 2.6 * sc, RadiusX = 1.3 * sc, RadiusY = 1.3 * sc, Fill = new SolidColorBrush(Color.FromRgb(0x8B, 0xB6, 0xFF)) };
                    Canvas.SetLeft(bar, ix - 6 * sc); Canvas.SetTop(bar, barTop + barH - 5 * sc);
                    _icons.Children.Add(bar);
                }
            }

            // tray: three dots + clock
            double trayRight = totalW - (s.Floating ? s.SideGap * sc : 0) - 14 * sc;
            var clock = new TextBlock
            {
                Text = "14:32\n04.11.2026", FontSize = Math.Max(5, 10.5 * sc), TextAlignment = TextAlignment.Right, LineHeight = Math.Max(6, 12 * sc),
                LineStackingStrategy = LineStackingStrategy.BlockLineHeight, Foreground = Brushes.White, FontWeight = FontWeights.SemiBold
            };
            clock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(clock, trayRight - clock.DesiredSize.Width); Canvas.SetTop(clock, cy - clock.DesiredSize.Height / 2);
            _icons.Children.Add(clock);
            for (int i = 0; i < 3; i++)
            {
                var d = new Ellipse { Width = 9 * sc, Height = 9 * sc, Fill = new SolidColorBrush(Color.FromArgb(0xCC, 255, 255, 255)) };
                Canvas.SetLeft(d, trayRight - clock.DesiredSize.Width - 20 * sc - i * 16 * sc); Canvas.SetTop(d, cy - 4.5 * sc);
                _icons.Children.Add(d);
            }
        }

        // ---------------- backdrop ----------------

        ImageSource BuildBackdrop(Rect rect)
        {
            // rendered once per Rebuild into a bitmap so it can also be blurred as an Image inside GlassPillVisual
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                var full = new Rect(0, 0, rect.Width, rect.Height);
                if (_backdropMode == 0 && GetWallpaper() is ImageSource wp)
                {
                    dc.DrawImage(wp, FitCover(wp, full));
                }
                else if (_backdropMode == 2) DrawLight(dc, full);
                else DrawAurora(dc, full);

                // mock app window so blur/transparency is visible
                double ww = full.Width * 0.42, wh = full.Height * 0.52;
                var win = new Rect(full.Width * 0.5 - ww / 2, full.Height * 0.38, ww, wh);
                var winBrush = new LinearGradientBrush(Color.FromRgb(0xFF, 0xB0, 0x6B), Color.FromRgb(0xE0, 0x4F, 0x9B), 35);
                dc.DrawRoundedRectangle(winBrush, null, win, 14 * rect.Width / VirtualW, 14 * rect.Width / VirtualW);
                var title = new Rect(win.X, win.Y, win.Width, 26 * rect.Width / VirtualW);
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(70, 255, 255, 255)), null, title, 14 * rect.Width / VirtualW, 14 * rect.Width / VirtualW);
            }
            var bmp = new RenderTargetBitmap(Math.Max(1, (int)rect.Width), Math.Max(1, (int)rect.Height), 96, 96, PixelFormats.Pbgra32);
            bmp.Render(visual);
            bmp.Freeze();
            return bmp;
        }

        static Rect FitCover(ImageSource img, Rect area)
        {
            double ar = img.Width / img.Height, tr = area.Width / area.Height;
            if (ar > tr) { double w = area.Height * ar; return new Rect(area.X - (w - area.Width) / 2, area.Y, w, area.Height); }
            double h = area.Width / ar; return new Rect(area.X, area.Y - (h - area.Height) / 2, area.Width, h);
        }

        static void DrawAurora(DrawingContext dc, Rect r)
        {
            dc.DrawRectangle(new LinearGradientBrush(Color.FromRgb(0x0B, 0x10, 0x2A), Color.FromRgb(0x1B, 0x0F, 0x3A), 60), null, r);
            void Blob(double fx, double fy, double fr, string c)
            {
                var col = ColorUtil.Parse(c);
                dc.DrawEllipse(new RadialGradientBrush(ColorUtil.WithAlpha(col, 0.85), ColorUtil.WithAlpha(col, 0)), null,
                    new Point(r.Width * fx, r.Height * fy), r.Width * fr, r.Width * fr);
            }
            Blob(0.2, 0.3, 0.35, "#5B7CFF"); Blob(0.8, 0.25, 0.3, "#C026D3"); Blob(0.55, 0.9, 0.4, "#06B6D4");
        }

        static void DrawLight(DrawingContext dc, Rect r)
        {
            dc.DrawRectangle(new LinearGradientBrush(Color.FromRgb(0xF4, 0xF1, 0xEA), Color.FromRgb(0xCF, 0xE3, 0xF7), 70), null, r);
            dc.DrawEllipse(new RadialGradientBrush(Color.FromArgb(200, 255, 190, 140), Color.FromArgb(0, 255, 190, 140)), null,
                new Point(r.Width * 0.25, r.Height * 0.3), r.Width * 0.3, r.Width * 0.3);
        }

        /// <summary>The user's real wallpaper (read-only, never leaves the machine), if it can be decoded.</summary>
        static ImageSource GetWallpaper()
        {
            if (_wallpaperLoaded) return _wallpaper;
            _wallpaperLoaded = true;
            try
            {
                string path = Registry.GetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop", "WallPaper", null) as string;
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                    path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Windows\Themes\TranscodedWallpaper");
                if (!File.Exists(path)) return null;
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.DecodePixelWidth = 1600;
                bi.UriSource = new Uri(path);
                bi.EndInit();
                bi.Freeze();
                _wallpaper = bi;
            }
            catch { _wallpaper = null; }
            return _wallpaper;
        }
    }
}
