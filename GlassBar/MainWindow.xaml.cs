using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace GlassBar
{
    public partial class MainWindow : Window
    {
        const double BarHeight = 56;

        readonly ObservableCollection<TaskItem> _tasks = new ObservableCollection<TaskItem>();
        readonly ObservableCollection<PinnedApp> _pinned = new ObservableCollection<PinnedApp>();
        readonly Dictionary<IntPtr, TaskItem> _byHwnd = new Dictionary<IntPtr, TaskItem>();
        readonly DispatcherTimer _fast = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        readonly DispatcherTimer _slow = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };

        IntPtr _hwnd;
        uint _callbackMsg, _taskbarCreatedMsg;
        int _tick;
        bool _registered;
        Forms.NotifyIcon _tray;
        SettingsWindow _settingsWindow;

        // CPU-Messung
        ulong _lastIdle, _lastKernel, _lastUser;

        static AppSettings S => AppSettings.Current;

        public MainWindow()
        {
            InitializeComponent();
            PinnedList.ItemsSource = _pinned;
            RunningList.ItemsSource = _tasks;
            _fast.Tick += (s, e) => { RefreshTasks(); if (S.HideNativeTaskbar) NativeTaskbar.Hide(); };
            _slow.Tick += (s, e) => RefreshStats();
        }

        // ---------- Start / Fenster-Setup ----------

        void Window_SourceInitialized(object sender, EventArgs e)
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            var src = HwndSource.FromHwnd(_hwnd);
            src.CompositionTarget.BackgroundColor = Colors.Transparent;
            src.AddHook(WndProc);

            // Nicht aktivierbar + nicht in Alt+Tab
            long ex = Native.GetWindowLongPtr(_hwnd, Native.GWL_EXSTYLE).ToInt64();
            Native.SetWindowLongPtr(_hwnd, Native.GWL_EXSTYLE, (IntPtr)(ex | Native.WS_EX_NOACTIVATE | Native.WS_EX_TOOLWINDOW));

            // Keine Windows-11-Rundung am Fensterrand
            int pref = 1; // DWMWCP_DONOTROUND
            Native.DwmSetWindowAttribute(_hwnd, 33, ref pref, 4);

            RegisterBar();
            ApplySettings();
            CreateTray();
        }

        void Window_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshStats();
            RefreshTasks();
            _fast.Start();
            _slow.Start();
        }

        // ---------- AppBar (reserviert Platz am Bildschirmrand) ----------

        Native.APPBARDATA NewAbd() => new Native.APPBARDATA
        {
            cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Native.APPBARDATA>(),
            hWnd = _hwnd
        };

        void RegisterBar()
        {
            var abd = NewAbd();
            _callbackMsg = Native.RegisterWindowMessage("GlassBarAppBarCallback");
            _taskbarCreatedMsg = Native.RegisterWindowMessage("TaskbarCreated");
            abd.uCallbackMessage = _callbackMsg;
            Native.SHAppBarMessage(Native.ABM_NEW, ref abd);
            _registered = true;
            PositionBar();
        }

        void PositionBar()
        {
            if (!_registered) return;
            int sw = Native.GetSystemMetrics(0), sh = Native.GetSystemMetrics(1);
            int h = (int)Math.Ceiling(BarHeight * VisualTreeHelper.GetDpi(this).DpiScaleY);

            var abd = NewAbd();
            abd.uEdge = S.Top ? Native.ABE_TOP : Native.ABE_BOTTOM;
            abd.rc = new Native.RECT { Left = 0, Top = 0, Right = sw, Bottom = sh };
            Native.SHAppBarMessage(Native.ABM_QUERYPOS, ref abd);
            if (S.Top) abd.rc.Bottom = abd.rc.Top + h; else abd.rc.Top = abd.rc.Bottom - h;
            Native.SHAppBarMessage(Native.ABM_SETPOS, ref abd);

            Native.SetWindowPos(_hwnd, Native.HWND_TOPMOST, abd.rc.Left, abd.rc.Top,
                abd.rc.Right - abd.rc.Left, abd.rc.Bottom - abd.rc.Top, Native.SWP_NOACTIVATE | Native.SWP_SHOWWINDOW);
            EdgeLine.VerticalAlignment = S.Top ? VerticalAlignment.Bottom : VerticalAlignment.Top;
        }

        IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (_callbackMsg != 0 && (uint)msg == _callbackMsg)
            {
                int n = wParam.ToInt32();
                if (n == Native.ABN_POSCHANGED) PositionBar();
                else if (n == Native.ABN_FULLSCREENAPP)
                {
                    // Vollbild-App (Spiel/Video): Leiste in den Hintergrund, danach wieder nach vorn
                    Native.SetWindowPos(_hwnd, lParam != IntPtr.Zero ? Native.HWND_BOTTOM : Native.HWND_TOPMOST,
                        0, 0, 0, 0, Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
                }
            }
            else if (_taskbarCreatedMsg != 0 && (uint)msg == _taskbarCreatedMsg)
            {
                // Explorer wurde neu gestartet: AppBar neu anmelden und Taskleiste erneut ausblenden
                NativeTaskbar.ForgetState();
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    var abd = NewAbd();
                    abd.uCallbackMessage = _callbackMsg;
                    Native.SHAppBarMessage(Native.ABM_NEW, ref abd);
                    PositionBar();
                    if (S.HideNativeTaskbar) NativeTaskbar.Hide();
                }));
            }
            else if (msg == 0x007E || msg == 0x02E0) // WM_DISPLAYCHANGE / WM_DPICHANGED
                Dispatcher.BeginInvoke(new Action(PositionBar));
            return IntPtr.Zero;
        }

        // ---------- Einstellungen anwenden ----------

        public void ApplySettings()
        {
            var tint = AppSettings.Tints.TryGetValue(S.Tint, out var c) ? c : AppSettings.Tints["Blau"];
            Native.EnableAcrylic(_hwnd, tint[0], tint[1], tint[2], (byte)Math.Max(0, Math.Min(255, S.TintAlpha)));

            StatsPanel.Visibility = S.ShowStats ? Visibility.Visible : Visibility.Collapsed;

            _pinned.Clear();
            foreach (var p in S.Pinned) _pinned.Add(p);

            if (S.HideNativeTaskbar) NativeTaskbar.Hide();
            else if (NativeTaskbar.IsHidden) NativeTaskbar.Restore();

            AutoStart.Set(S.AutoStart);
            PositionBar();
            RefreshStats();
            RefreshTasks();
            S.Save();
        }

        // ---------- Fensterliste ----------

        PinnedApp FindPinned(string exe) =>
            exe == null ? null : _pinned.FirstOrDefault(p => string.Equals(p.Path, exe, StringComparison.OrdinalIgnoreCase));

        void RefreshTasks()
        {
            _tick++;
            var windows = WindowTracker.GetTaskWindows(_hwnd);
            var set = new HashSet<IntPtr>(windows);
            IntPtr active = Native.GetAncestor(Native.GetForegroundWindow(), 3);

            foreach (var gone in _byHwnd.Keys.Where(h => !set.Contains(h)).ToList())
            {
                _tasks.Remove(_byHwnd[gone]);
                _byHwnd.Remove(gone);
            }

            foreach (var p in _pinned) p.Windows.Clear();
            var pinnedActive = new HashSet<PinnedApp>();

            foreach (var h in windows)
            {
                if (!_byHwnd.TryGetValue(h, out var item))
                {
                    item = new TaskItem { Hwnd = h, ExePath = Native.GetExePath(h) };
                    _byHwnd[h] = item;
                }
                if (item.Icon == null && (_tick % 10 == 0 || !_tasks.Contains(item))) item.Icon = IconHelper.ForWindow(h);
                item.Title = Native.GetTitle(h);
                item.IsActive = h == active && !Native.IsIconic(h);

                var pin = FindPinned(item.ExePath);
                if (pin != null)
                {
                    // Fenster einer angehefteten App werden dort gebündelt statt doppelt angezeigt
                    pin.Windows.Add(h);
                    if (item.IsActive) pinnedActive.Add(pin);
                    _tasks.Remove(item);
                }
                else if (!_tasks.Contains(item)) _tasks.Add(item);
            }

            foreach (var p in _pinned)
            {
                p.IsRunning = p.Windows.Count > 0;
                p.IsActive = pinnedActive.Contains(p);
            }
            UpdateItemWidths();
        }

        void UpdateItemWidths()
        {
            if (_tasks.Count == 0) return;
            double avail = RunningList.ActualWidth - 4;
            double w = Math.Max(44, Math.Min(170, avail / _tasks.Count - 4));
            foreach (var t in _tasks) t.ItemWidth = w;
        }

        void RunningList_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateItemWidths();

        TaskItem ItemOf(object sender) => (TaskItem)((FrameworkElement)sender).DataContext;

        void Task_Click(object sender, RoutedEventArgs e) => WindowTracker.Activate(ItemOf(sender).Hwnd);
        void Task_Minimize(object sender, RoutedEventArgs e) => Native.ShowWindow(ItemOf(sender).Hwnd, Native.SW_MINIMIZE);
        void Task_Close(object sender, RoutedEventArgs e) => Native.PostMessage(ItemOf(sender).Hwnd, Native.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);

        // ---------- Angeheftete Apps ----------

        static void Launch(PinnedApp app)
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(app.Path) { UseShellExecute = true }); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "GlassBar"); }
        }

        void Pinned_Click(object sender, RoutedEventArgs e)
        {
            var app = (PinnedApp)((FrameworkElement)sender).DataContext;
            if (app.Windows.Count == 0) { Launch(app); return; }
            // Läuft schon: zum Fenster wechseln (bei mehreren Fenstern durchschalten)
            IntPtr fg = Native.GetAncestor(Native.GetForegroundWindow(), 3);
            int idx = app.Windows.IndexOf(fg);
            WindowTracker.Activate(app.Windows.Count == 1 ? app.Windows[0] : app.Windows[(idx + 1) % app.Windows.Count]);
        }

        void Pinned_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Middle) return;
            Launch((PinnedApp)((FrameworkElement)sender).DataContext); // Mittelklick = neue Instanz
            e.Handled = true;
        }

        void Task_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Middle) return;
            Native.PostMessage(ItemOf(sender).Hwnd, Native.WM_CLOSE, IntPtr.Zero, IntPtr.Zero); // Mittelklick = schließen
            e.Handled = true;
        }

        void Unpin_Click(object sender, RoutedEventArgs e)
        {
            var app = (PinnedApp)((FrameworkElement)sender).DataContext;
            S.Pinned.Remove(app);
            ApplySettings();
        }

        void Window_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            foreach (var f in (string[])e.Data.GetData(DataFormats.FileDrop))
            {
                if (S.Pinned.Any(p => string.Equals(p.Path, f, StringComparison.OrdinalIgnoreCase))) continue;
                S.Pinned.Add(new PinnedApp { Name = System.IO.Path.GetFileNameWithoutExtension(f), Path = f });
            }
            ApplySettings();
        }

        // ---------- Statusanzeigen ----------

        void RefreshStats()
        {
            var now = DateTime.Now;
            string time = S.Clock24h ? now.ToString("HH:mm:ss") : now.ToString("hh:mm tt");
            ClockText.Text = time + "\n" + now.ToString("dd.MM.yyyy");

            if (!S.ShowStats) return;

            if (Native.GetSystemTimes(out var idle, out var kernel, out var user))
            {
                ulong i = idle.Value, k = kernel.Value, u = user.Value;
                ulong total = (k - _lastKernel) + (u - _lastUser);
                if (_lastKernel != 0 && total > 0)
                {
                    double cpu = 100.0 * (1.0 - (double)(i - _lastIdle) / total);
                    CpuText.Text = $"CPU {Math.Max(0, Math.Min(100, cpu)):0}%";
                }
                _lastIdle = i; _lastKernel = k; _lastUser = u;
            }

            var mem = new Native.MEMORYSTATUSEX { dwLength = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Native.MEMORYSTATUSEX>() };
            if (Native.GlobalMemoryStatusEx(ref mem)) RamText.Text = $"RAM {mem.dwMemoryLoad}%";

            var ps = Forms.SystemInformation.PowerStatus;
            if ((ps.BatteryChargeStatus & Forms.BatteryChargeStatus.NoSystemBattery) != 0)
                BatText.Visibility = Visibility.Collapsed;
            else
            {
                BatText.Visibility = Visibility.Visible;
                bool charging = ps.PowerLineStatus == Forms.PowerLineStatus.Online;
                BatText.Text = (charging ? "⚡" : "🔋") + $"{Math.Min(100, ps.BatteryLifePercent * 100):0}%";
            }
        }

        // ---------- Schnellaktionen ----------

        const byte VK_LWIN = 0x5B;

        void Start_Click(object s, RoutedEventArgs e) => Native.SendKeys(VK_LWIN);
        void Search_Click(object s, RoutedEventArgs e) => Native.SendKeys(VK_LWIN, (byte)'S');
        void TaskView_Click(object s, RoutedEventArgs e) => Native.SendKeys(VK_LWIN, 0x09); // Win+Tab
        void Clock_Click(object s, RoutedEventArgs e) => Native.SendKeys(VK_LWIN, (byte)'N');
        void ShowDesktop_Click(object s, RoutedEventArgs e) => Native.SendKeys(VK_LWIN, (byte)'D');
        void VolUp_Click(object s, RoutedEventArgs e) => Native.SendKeys(0xAF);
        void VolDown_Click(object s, RoutedEventArgs e) => Native.SendKeys(0xAE);
        void VolMute_Click(object s, RoutedEventArgs e) => Native.SendKeys(0xAD);

        void Volume_Wheel(object s, MouseWheelEventArgs e)
        {
            if (e.Delta > 0) Native.SendKeys(0xAF); else Native.SendKeys(0xAE);
        }

        // ---------- Einstellungen / Tray ----------

        void Settings_Click(object s, RoutedEventArgs e) => OpenSettings();

        public void OpenSettings()
        {
            if (_settingsWindow != null) { _settingsWindow.Activate(); return; }
            _settingsWindow = new SettingsWindow(this);
            _settingsWindow.Closed += (s, e) => _settingsWindow = null;
            _settingsWindow.Show();
        }

        void CreateTray()
        {
            var menu = new Forms.ContextMenuStrip();
            menu.Items.Add("Einstellungen", null, (s, e) => OpenSettings());
            menu.Items.Add("Windows-Taskleiste ein/aus", null, (s, e) =>
            {
                S.HideNativeTaskbar = !S.HideNativeTaskbar;
                ApplySettings();
            });
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add("Beenden", null, (s, e) => Application.Current.Shutdown());
            _tray = new Forms.NotifyIcon
            {
                Icon = System.Drawing.SystemIcons.Application,
                Text = "GlassBar",
                Visible = true,
                ContextMenuStrip = menu
            };
            _tray.DoubleClick += (s, e) => OpenSettings();
        }

        protected override void OnClosed(EventArgs e)
        {
            _fast.Stop(); _slow.Stop();
            if (_registered)
            {
                var abd = NewAbd();
                Native.SHAppBarMessage(Native.ABM_REMOVE, ref abd);
                _registered = false;
            }
            if (NativeTaskbar.IsHidden) NativeTaskbar.Restore();
            if (_tray != null) { _tray.Visible = false; _tray.Dispose(); }
            S.Save();
            base.OnClosed(e);
        }
    }
}
