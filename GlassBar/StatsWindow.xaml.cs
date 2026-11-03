using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace GlassBar
{
    /// <summary>Flyout mit Live-Graphen, Top-Prozessen, Smart-Check und optionaler KI-Analyse.</summary>
    public partial class StatsWindow : Window
    {
        const int Points = 60;
        const double W = 320, H = 44;

        readonly DispatcherTimer _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        readonly List<double> _cpuHist = new List<double>(), _ramHist = new List<double>();
        ulong _lastIdle, _lastKernel, _lastUser;
        long _lastRx, _lastTx;
        int _tick;

        // Prozess-Messung
        Dictionary<int, TimeSpan> _procCpu = new Dictionary<int, TimeSpan>();
        DateTime _procStamp = DateTime.Now;
        List<ProcInfo> _top = new List<ProcInfo>();
        double _cpu, _ram;
        string _net = "";

        class ProcInfo { public string Name; public double Cpu; public long MemMb; }

        public StatsWindow()
        {
            InitializeComponent();
            _timer.Tick += (s, e) => Sample();
            Loaded += (s, e) => { Sample(); _timer.Start(); };
            Closed += (s, e) => _timer.Stop();
        }

        public void PositionNear(double anchorXpx, double barEdgePx, bool barOnTop)
        {
            var hwnd = new WindowInteropHelper(this).EnsureHandle();
            HwndSource.FromHwnd(hwnd).CompositionTarget.BackgroundColor = Colors.Transparent;
            Native.MakeToolWindow(hwnd, false, false);
            Native.EnableAcrylic(hwnd, 20, 28, 50, 130);

            double dpi = VisualTreeHelper.GetDpi(this).DpiScaleX;
            int wpx = (int)(Width * dpi), hpx = (int)(Height * dpi);
            int x = (int)(anchorXpx - wpx / 2.0);
            x = Math.Max(8, Math.Min(Native.GetSystemMetrics(0) - wpx - 8, x));
            int gap = (int)(10 * dpi);
            int y = barOnTop ? (int)barEdgePx + gap : (int)barEdgePx - hpx - gap;
            Native.SetWindowPos(hwnd, Native.HWND_TOPMOST, x, y, wpx, hpx, Native.SWP_NOACTIVATE);
            Native.RoundCorners(hwnd, wpx, hpx, (int)(22 * dpi));
        }

        void Window_Deactivated(object sender, EventArgs e) { Dispatcher.BeginInvoke(new Action(Close)); }

        // ---------- Messung ----------

        void Sample()
        {
            _tick++;

            if (Native.GetSystemTimes(out var idle, out var kernel, out var user))
            {
                ulong i = idle.Value, k = kernel.Value, u = user.Value;
                ulong total = (k - _lastKernel) + (u - _lastUser);
                if (_lastKernel != 0 && total > 0)
                    _cpu = Math.Max(0, Math.Min(100, 100.0 * (1.0 - (double)(i - _lastIdle) / total)));
                _lastIdle = i; _lastKernel = k; _lastUser = u;
            }

            var mem = new Native.MEMORYSTATUSEX { dwLength = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Native.MEMORYSTATUSEX>() };
            double usedGb = 0, totalGb = 0;
            if (Native.GlobalMemoryStatusEx(ref mem))
            {
                _ram = mem.dwMemoryLoad;
                totalGb = mem.ullTotalPhys / 1073741824.0;
                usedGb = totalGb - mem.ullAvailPhys / 1073741824.0;
            }

            Push(_cpuHist, _cpu); Push(_ramHist, _ram);
            Draw(CpuArea, _cpuHist); Draw(RamArea, _ramHist);
            CpuLabel.Text = $"CPU  {_cpu:0}%";
            RamLabel.Text = $"RAM  {_ram:0}%   ({usedGb:0.0} / {totalGb:0.0} GB)";

            SampleNetwork();
            if (_tick % 3 == 1) SampleProcesses();
            UpdateInsight();
        }

        static void Push(List<double> list, double v)
        {
            list.Add(v);
            if (list.Count > Points) list.RemoveAt(0);
        }

        static void Draw(Polygon poly, List<double> data)
        {
            var pts = new PointCollection { new Point(W - (data.Count - 1) * (W / (Points - 1)), H) };
            for (int i = 0; i < data.Count; i++)
                pts.Add(new Point(W - (data.Count - 1 - i) * (W / (Points - 1)), H - 2 - data[i] / 100.0 * (H - 4)));
            pts.Add(new Point(W, H));
            poly.Points = pts;
        }

        void SampleNetwork()
        {
            try
            {
                long rx = 0, tx = 0;
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    var st = nic.GetIPv4Statistics();
                    rx += st.BytesReceived; tx += st.BytesSent;
                }
                if (_lastRx != 0)
                    _net = $"↓ {Fmt(rx - _lastRx)}/s    ↑ {Fmt(tx - _lastTx)}/s";
                _lastRx = rx; _lastTx = tx;
                NetLabel.Text = "Netzwerk  " + _net;
            }
            catch { NetLabel.Text = "Netzwerk  –"; }
        }

        static string Fmt(long bytes)
        {
            if (bytes < 0) bytes = 0;
            if (bytes >= 1048576) return (bytes / 1048576.0).ToString("0.0") + " MB";
            return (bytes / 1024.0).ToString("0") + " KB";
        }

        void SampleProcesses()
        {
            try
            {
                var now = DateTime.Now;
                double elapsed = (now - _procStamp).TotalSeconds;
                var newCpu = new Dictionary<int, TimeSpan>();
                var groups = new Dictionary<string, ProcInfo>();
                foreach (var p in Process.GetProcesses())
                {
                    using (p)
                    {
                        try
                        {
                            var t = p.TotalProcessorTime;
                            newCpu[p.Id] = t;
                            double cpu = 0;
                            if (elapsed > 0 && _procCpu.TryGetValue(p.Id, out var prev))
                                cpu = (t - prev).TotalSeconds / elapsed / Environment.ProcessorCount * 100.0;
                            long mb = p.WorkingSet64 / 1048576;
                            if (!groups.TryGetValue(p.ProcessName, out var g)) groups[p.ProcessName] = g = new ProcInfo { Name = p.ProcessName };
                            g.Cpu += cpu; g.MemMb += mb;
                        }
                        catch { /* Zugriff verweigert – System-Prozess */ }
                    }
                }
                _procCpu = newCpu; _procStamp = now;
                _top = groups.Values.OrderByDescending(g => g.Cpu * 50 + g.MemMb).Take(5).ToList();
                ProcText.Text = string.Join("\n", _top.Select(g => $"{g.Name}  —  {g.Cpu:0.0}% CPU · {g.MemMb} MB"));
            }
            catch { }
        }

        // ---------- Smart-Check (lokal) + KI ----------

        void UpdateInsight()
        {
            string topCpu = _top.OrderByDescending(g => g.Cpu).Select(g => g.Name).FirstOrDefault() ?? "–";
            string topMem = _top.OrderByDescending(g => g.MemMb).Select(g => g.Name).FirstOrDefault() ?? "–";
            string text;
            if (_cpu > 85) text = $"⚠ Hohe CPU-Last. Größter Verbraucher: {topCpu}.";
            else if (_ram > 85) text = $"⚠ Arbeitsspeicher knapp. Größter Verbraucher: {topMem}. Nicht benötigte Programme schließen.";
            else if (_cpu < 25 && _ram < 70) text = "✓ System läuft entspannt – genug Reserven.";
            else text = $"Normale Auslastung. Meiste CPU: {topCpu}, meister RAM: {topMem}.";
            InsightText.Text = "Smart-Check: " + text;
        }

        async void Ai_Click(object sender, RoutedEventArgs e)
        {
            AiButton.IsEnabled = false;
            AiText.Text = "KI analysiert …";
            var sb = new StringBuilder();
            sb.AppendLine($"CPU: {_cpu:0}%, RAM: {_ram:0}%, Netzwerk: {_net}");
            sb.AppendLine($"CPU-Verlauf (letzte Messwerte): {string.Join(", ", _cpuHist.Skip(Math.Max(0, _cpuHist.Count - 20)).Select(v => v.ToString("0")))}");
            sb.AppendLine("Top-Prozesse:");
            foreach (var g in _top) sb.AppendLine($"- {g.Name}: {g.Cpu:0.0}% CPU, {g.MemMb} MB");
            string result = await AiAnalyzer.AskAsync(sb.ToString());
            AiText.Text = "✨ " + result;
            AiButton.IsEnabled = true;
        }
    }
}
