using System.Windows;
using System.Windows.Controls;

namespace GlassBar
{
    public partial class SettingsWindow : Window
    {
        readonly MainWindow _main;

        public SettingsWindow(MainWindow main)
        {
            InitializeComponent();
            _main = main;
            var s = AppSettings.Current;
            AutoStartBox.IsChecked = s.AutoStart;
            HideBox.IsChecked = s.HideNativeTaskbar;
            StatsBox.IsChecked = s.ShowStats;
            Clock24Box.IsChecked = s.Clock24h;
            PosBox.SelectedIndex = s.Top ? 1 : 0;
            foreach (var name in AppSettings.Tints.Keys) TintBox.Items.Add(name);
            TintBox.SelectedItem = s.Tint;
            if (TintBox.SelectedIndex < 0) TintBox.SelectedIndex = 0;
            AlphaSlider.Value = s.TintAlpha;
            KeyBox.Password = s.ApiKey ?? "";
        }

        void Apply_Click(object sender, RoutedEventArgs e)
        {
            var s = AppSettings.Current;
            s.AutoStart = AutoStartBox.IsChecked == true;
            s.HideNativeTaskbar = HideBox.IsChecked == true;
            s.ShowStats = StatsBox.IsChecked == true;
            s.Clock24h = Clock24Box.IsChecked == true;
            s.Top = PosBox.SelectedIndex == 1;
            s.Tint = (string)TintBox.SelectedItem;
            s.TintAlpha = (int)AlphaSlider.Value;
            s.ApiKey = KeyBox.Password;
            _main.ApplySettings();
        }

        void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
