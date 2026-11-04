using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using GlassTaskbarStudio.Core;
using GlassTaskbarStudio.UI.Pages;

namespace GlassTaskbarStudio.UI
{
    public partial class MainWindow : Window
    {
        readonly MainViewModel _vm;
        readonly UserControl[] _pages;
        uint _taskbarCreatedMsg;

        public bool AllowClose { get; set; }

        public MainWindow(MainViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
            DataContext = vm;
            Icon = IconFactory.CreateBitmap(64);
            LogoImage.Source = IconFactory.CreateBitmap(48);

            // fit small screens
            Height = Math.Min(Height, SystemParameters.WorkArea.Height - 24);

            _pages = new UserControl[]
            {
                new DashboardPage(), new AppearancePage(), new TransparencyPage(), new ColorsPage(),
                new LayoutPage(), new AnimationsPage(), new PresetsPage(), new AdvancedPage()
            };
            string[] titles = { "Dashboard", "Appearance", "Transparency & Blur", "Colors", "Position & Layout", "Animations", "Presets", "Advanced" };
            string[] icons = { "IconDashboard", "IconBrush", "IconDrop", "IconPalette", "IconLayout", "IconMotion", "IconSparkle", "IconTools" };
            for (int i = 0; i < _pages.Length; i++)
            {
                int index = i;
                var rb = new RadioButton
                {
                    Content = titles[i], Style = (Style)FindResource("NavItem"), GroupName = "nav", Tag = (Geometry)FindResource(icons[i])
                };
                rb.Checked += (s, e) => Navigate(index);
                NavPanel.Children.Add(rb);
                if (i == 0) rb.IsChecked = true;
            }
        }

        void Navigate(int index)
        {
            PageHost.Content = _pages[index];
            if (!_vm.UiAnimations) return;
            // soft fade + slide-up
            _pages[index].Opacity = 0;
            var slide = new TranslateTransform(0, 14);
            _pages[index].RenderTransform = slide;
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            _pages[index].BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(260)) { EasingFunction = ease });
            slide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(14, 0, TimeSpan.FromMilliseconds(320)) { EasingFunction = ease });
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var hwnd = new WindowInteropHelper(this).Handle;
            int on = 1, round = 2;
            Native.DwmSetWindowAttribute(hwnd, 20, ref on, 4);      // dark title/frame
            Native.DwmSetWindowAttribute(hwnd, 33, ref round, 4);   // rounded corners (Windows 11)
            _taskbarCreatedMsg = Native.RegisterWindowMessage("TaskbarCreated");
            HwndSource.FromHwnd(hwnd).AddHook(WndProc);
        }

        IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (_taskbarCreatedMsg != 0 && (uint)msg == _taskbarCreatedMsg) _vm.Engine.OnExplorerRestarted();
            else if (msg == 0x007E) _vm.Engine.OnDisplayChanged();   // WM_DISPLAYCHANGE
            return IntPtr.Zero;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!AllowClose) { e.Cancel = true; Hide(); return; } // closing hides to the tray; effects keep running
            base.OnClosing(e);
        }

        void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
