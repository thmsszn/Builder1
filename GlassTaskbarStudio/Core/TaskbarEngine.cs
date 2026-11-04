using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace GlassTaskbarStudio.Core
{
    /// <summary>
    /// Applies a <see cref="TaskbarSettings"/> to the REAL Windows taskbar(s):
    ///  - native:  accent policy on Shell_TrayWnd (clear / blur / acrylic / opaque + tint)      [SetWindowCompositionAttribute]
    ///  - region:  rounded corners, floating margins, compact "islands"                          [SetWindowRgn]
    ///  - overlay: gloss, rim light, shadow, shimmer in a click-through window                   [OverlayWindow]
    /// Explorer resets some of this whenever it repaints, so a light timer re-asserts it. Everything is undone by Restore().
    /// </summary>
    public sealed class TaskbarEngine : IDisposable
    {
        sealed class Bar
        {
            public IntPtr Hwnd;
            public OverlayWindow Overlay;
            public string RegionKey;
            public Rect? AppGroupScreen;
            public bool Measuring;
            public bool HasRegion;
        }

        readonly Dictionary<IntPtr, Bar> _bars = new Dictionary<IntPtr, Bar>();
        readonly DispatcherTimer _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000) };
        readonly DispatcherTimer _anim = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        readonly Dispatcher _ui = Application.Current.Dispatcher;
        readonly IntPtr _probeRgn = Native.CreateRectRgn(0, 0, 0, 0);

        TaskbarSettings _target = Presets.Default.Settings.Clone(), _shown = Presets.Default.Settings.Clone(), _from;
        DateTime _animStart;
        bool _active, _fullscreen, _disposed;
        string _status = "Idle";

        public bool HideInFullscreen { get; set; } = true;
        public bool IsActive => _active;
        public string Status { get => _status; private set { if (_status != value) { _status = value; StatusChanged?.Invoke(value); } } }
        public event Action<string> StatusChanged;

        public int RefreshMs { set => _timer.Interval = TimeSpan.FromMilliseconds(Math.Max(250, Math.Min(10000, value))); }

        public TaskbarEngine()
        {
            _timer.Tick += (s, e) => Tick();
            _anim.Tick += (s, e) => AnimTick();
        }

        // ---------------- public API ----------------

        /// <summary>Starts or resumes rendering the current settings on the real taskbar.</summary>
        public void Enable()
        {
            if (_disposed) return;
            _active = true;
            _timer.Start();
            SyncBars();
            RenderAll();
        }

        /// <summary>Stops all effects and gives the taskbar back to Windows.</summary>
        public void Disable()
        {
            _active = false;
            _timer.Stop(); _anim.Stop();
            foreach (var b in _bars.Values.ToList()) RestoreBar(b);
            _bars.Clear();
            Status = "Effects off - the original Windows taskbar is shown";
        }

        public void Apply(TaskbarSettings settings, bool animate)
        {
            var target = settings.Clone();
            _target = target;
            if (_active && animate && target.AnimateTransitions && _shown != null)
            {
                _from = _shown.Clone();
                _animStart = DateTime.UtcNow;
                _anim.Start();
            }
            else { _anim.Stop(); _shown = target.Clone(); }
            if (_active) { SyncBars(); RenderAll(); }
        }

        /// <summary>Explorer restarted (taskbar window was recreated).</summary>
        public void OnExplorerRestarted()
        {
            foreach (var b in _bars.Values) b.Overlay?.Close();
            _bars.Clear();
            Task.Delay(1500).ContinueWith(_ => _ui.BeginInvoke(new Action(() => { if (_active) { SyncBars(); RenderAll(); } })));
        }

        public void OnDisplayChanged()
        {
            foreach (var b in _bars.Values) { b.RegionKey = null; b.AppGroupScreen = null; }
            if (_active) { SyncBars(); RenderAll(); }
        }

        /// <summary>Emergency reset usable without a running engine (used by --reset).</summary>
        public static void HardRestoreAll()
        {
            foreach (var h in FindTaskbars(true)) RestoreHandle(h);
        }

        // ---------------- loop ----------------

        void Tick()
        {
            if (!_active) return;
            SyncBars();
            MeasureAsync();
            RenderAll();
        }

        void AnimTick()
        {
            double t = (DateTime.UtcNow - _animStart).TotalMilliseconds / Math.Max(1, _target.TransitionMs);
            if (t >= 1) { _anim.Stop(); _shown = _target.Clone(); }
            else
            {
                double e = 1 - Math.Pow(1 - t, 3); // ease-out cubic
                _shown = TaskbarSettings.Lerp(_from, _target, e);
            }
            if (_active) RenderAll();
        }

        static List<IntPtr> FindTaskbars(bool all)
        {
            var list = new List<IntPtr>();
            var main = Native.FindWindow("Shell_TrayWnd", null);
            if (main != IntPtr.Zero) list.Add(main);
            if (all)
            {
                IntPtr sec = IntPtr.Zero;
                while ((sec = Native.FindWindowEx(IntPtr.Zero, sec, "Shell_SecondaryTrayWnd", null)) != IntPtr.Zero) list.Add(sec);
            }
            return list;
        }

        void SyncBars()
        {
            var current = FindTaskbars(_target.AllMonitors);
            foreach (var gone in _bars.Keys.Where(h => !current.Contains(h)).ToList())
            {
                RestoreBar(_bars[gone]);
                _bars.Remove(gone);
            }
            foreach (var h in current)
                if (!_bars.ContainsKey(h)) _bars[h] = new Bar { Hwnd = h };
        }

        // ---------------- measuring the icon group (UI Automation, off the UI thread) ----------------

        void MeasureAsync()
        {
            if (!(_shown.Compact && _shown.AutoWidth && Capabilities.SupportsCompact)) return;
            bool centered = _shown.Alignment == IconAlignment.Center;
            foreach (var b in _bars.Values)
            {
                if (b.Measuring || !Native.GetWindowRect(b.Hwnd, out var rect)) continue;
                double dpi = Math.Max(1, Native.GetDpiForWindow(b.Hwnd) / 96.0);
                double trayLeft = TaskbarMeasure.TryGetTray(b.Hwnd, out var tr) ? tr.Left : rect.Right - 220 * dpi;
                var bar = b; b.Measuring = true;
                Task.Run(() => TaskbarMeasure.MeasureAppGroup(bar.Hwnd, rect, trayLeft, centered, dpi)).ContinueWith(t =>
                    _ui.BeginInvoke(new Action(() =>
                    {
                        bar.Measuring = false;
                        var r = t.Status == TaskStatus.RanToCompletion ? t.Result : null;
                        if (_disposed || !_active || !r.HasValue) return;
                        var old = bar.AppGroupScreen;
                        if (!old.HasValue || Math.Abs(old.Value.X - r.Value.X) > 2 || Math.Abs(old.Value.Width - r.Value.Width) > 2)
                        {
                            bar.AppGroupScreen = r;
                            RenderBar(bar, _shown);
                        }
                    })));
            }
        }

        // ---------------- rendering ----------------

        void RenderAll()
        {
            _fullscreen = HideInFullscreen && IsFullscreenAppActive();
            foreach (var b in _bars.Values) RenderBar(b, _shown);
            Status = _bars.Count == 0
                ? "Waiting for the Windows taskbar..."
                : $"Active on {_bars.Count} taskbar{(_bars.Count > 1 ? "s" : "")} - {_target.Material}" +
                  (_target.Compact && Capabilities.SupportsCompact ? (_target.AutoWidth ? ", compact (auto-fit)" : ", compact") : "");
        }

        void RenderBar(Bar b, TaskbarSettings s)
        {
            if (!Native.IsWindowVisible(b.Hwnd) || !Native.GetWindowRect(b.Hwnd, out var r) || r.Width <= 0 || r.Height <= 0)
            {
                b.Overlay?.HideOverlay();
                return;
            }
            double dpi = Math.Max(1, Native.GetDpiForWindow(b.Hwnd) / 96.0);
            double trayW = TaskbarMeasure.TryGetTray(b.Hwnd, out var tr) ? tr.Width : 220 * dpi;
            Rect? group = b.AppGroupScreen.HasValue ? new Rect(b.AppGroupScreen.Value.X - r.Left, 0, b.AppGroupScreen.Value.Width, r.Height) : (Rect?)null;

            var layout = LayoutCalculator.Compute(s, r.Width, r.Height, dpi, trayW, group, Capabilities.SupportsCompact);

            string key = $"{r.Left},{r.Top},{r.Width},{r.Height}|{layout.Key}";
            if (key != b.RegionKey || (b.HasRegion && Native.GetWindowRgn(b.Hwnd, _probeRgn) == 0))
            {
                b.HasRegion = ApplyRegion(b.Hwnd, layout, r);
                b.RegionKey = key;
            }
            ApplyAccent(b.Hwnd, s);

            if (_fullscreen) { b.Overlay?.HideOverlay(); return; }
            if (b.Overlay == null) b.Overlay = new OverlayWindow();
            b.Overlay.UpdateOverlay(r, layout, s, dpi);
        }

        static bool ApplyRegion(IntPtr hwnd, PillLayout layout, Native.RECT bar)
        {
            // A single pill covering the whole bar without rounding needs no region at all.
            if (layout.Pills.Count == 1 && layout.Radius < 0.5 &&
                layout.Pills[0].X < 1 && layout.Pills[0].Y < 1 &&
                layout.Pills[0].Width > bar.Width - 2 && layout.Pills[0].Height > bar.Height - 2)
            {
                Native.SetWindowRgn(hwnd, IntPtr.Zero, true);
                return false;
            }

            IntPtr rgn = IntPtr.Zero;
            int d = (int)Math.Round(layout.Radius * 2);
            foreach (var p in layout.Pills)
            {
                IntPtr one = Native.CreateRoundRectRgn((int)Math.Round(p.Left), (int)Math.Round(p.Top),
                                                      (int)Math.Round(p.Right) + 1, (int)Math.Round(p.Bottom) + 1, d, d);
                if (rgn == IntPtr.Zero) rgn = one;
                else { Native.CombineRgn(rgn, rgn, one, Native.RGN_OR); Native.DeleteObject(one); }
            }
            Native.SetWindowRgn(hwnd, rgn, true); // the system owns the region from here on
            return true;
        }

        static void ApplyAccent(IntPtr hwnd, TaskbarSettings s)
        {
            var c = ColorUtil.Parse(s.TintHex);
            byte a = (byte)Math.Max(1, Math.Round((100 - s.Transparency) / 100.0 * 255));
            switch (s.Material)
            {
                case TaskbarMaterial.Clear: Native.SetAccent(hwnd, 2, 0, 0, 0, 0); break;
                case TaskbarMaterial.Blur: Native.SetAccent(hwnd, 3, c.R, c.G, c.B, a); break;
                case TaskbarMaterial.Acrylic: Native.SetAccent(hwnd, Capabilities.SupportsAcrylic ? 4 : 3, c.R, c.G, c.B, a); break;
                default: Native.SetAccent(hwnd, 1, c.R, c.G, c.B, 255); break;
            }
        }

        // ---------------- restoring ----------------

        static void RestoreHandle(IntPtr hwnd)
        {
            Native.SetWindowRgn(hwnd, IntPtr.Zero, true);
            Native.SetAccent(hwnd, 0, 0, 0, 0, 0);
            // nudge Explorer to repaint with its own style (NOMOVE|NOSIZE|NOZORDER|NOACTIVATE|FRAMECHANGED)
            Native.SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, 0x1 | 0x2 | 0x4 | 0x10 | 0x20);
        }

        void RestoreBar(Bar b)
        {
            b.Overlay?.Close();
            b.Overlay = null;
            RestoreHandle(b.Hwnd);
        }

        static bool IsFullscreenAppActive()
        {
            var fg = Native.GetForegroundWindow();
            if (fg == IntPtr.Zero) return false;
            string cls = Native.GetClass(fg);
            if (cls == "Progman" || cls == "WorkerW" || cls == "Shell_TrayWnd" || cls == "Shell_SecondaryTrayWnd") return false;
            if (!Native.GetWindowRect(fg, out var r)) return false;
            var mon = Native.MonitorFromWindow(fg, 2);
            var mi = new Native.MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Native.MONITORINFO>() };
            if (!Native.GetMonitorInfo(mon, ref mi)) return false;
            return r.Left <= mi.rcMonitor.Left && r.Top <= mi.rcMonitor.Top && r.Right >= mi.rcMonitor.Right && r.Bottom >= mi.rcMonitor.Bottom;
        }

        public void Dispose()
        {
            if (_disposed) return;
            Disable();
            _disposed = true;
            Native.DeleteObject(_probeRgn);
        }
    }
}
