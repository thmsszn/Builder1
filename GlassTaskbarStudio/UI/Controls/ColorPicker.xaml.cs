using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using GlassTaskbarStudio.Core;

namespace GlassTaskbarStudio.UI.Controls
{
    /// <summary>Swatches + hex field + HSV sliders. Two-way bound through the Hex property ("#RRGGBB").</summary>
    public partial class ColorPicker : UserControl
    {
        public static readonly DependencyProperty HexProperty = DependencyProperty.Register(nameof(Hex), typeof(string), typeof(ColorPicker),
            new FrameworkPropertyMetadata("#FFFFFF", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, e) => ((ColorPicker)d).OnHexChanged()));

        static readonly string[] SwatchColors =
        {
            "#FFFFFF", "#C9D3E8", "#0A1226", "#05060B", "#000000", "#1C1C20",
            "#6F95C4", "#3FB0FF", "#00E5FF", "#34D399", "#A3E635", "#FBBF24",
            "#FB7185", "#F472B6", "#A78BFA", "#7C5CFF"
        };

        bool _updating;

        public string Hex { get => (string)GetValue(HexProperty); set => SetValue(HexProperty, value); }

        public ColorPicker()
        {
            InitializeComponent();
            Swatches.ItemsSource = SwatchColors;
            OnHexChanged();
        }

        void OnHexChanged()
        {
            if (_updating || HexBox == null) return;
            _updating = true;
            var c = ColorUtil.Parse(Hex);
            HexBox.Text = ColorUtil.ToHex(c);
            Preview.Background = new SolidColorBrush(c);
            ColorUtil.ToHsv(c, out double h, out double s, out double v);
            HueSlider.Value = h; SatSlider.Value = s * 100; ValSlider.Value = v * 100;
            _updating = false;
        }

        void SetHex(string hex)
        {
            if (!ColorUtil.IsValidHex(hex)) { HexBox.Text = ColorUtil.ToHex(ColorUtil.Parse(Hex)); return; }
            Hex = ColorUtil.ToHex(ColorUtil.Parse(hex));
        }

        void Swatch_Click(object sender, RoutedEventArgs e) => SetHex((string)((FrameworkElement)sender).Tag);
        void HexBox_LostFocus(object sender, RoutedEventArgs e) => SetHex(HexBox.Text.StartsWith("#") ? HexBox.Text : "#" + HexBox.Text);
        void HexBox_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) SetHex(HexBox.Text.StartsWith("#") ? HexBox.Text : "#" + HexBox.Text); }

        void Hsv_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_updating) return;
            _updating = true;
            var c = ColorUtil.FromHsv(HueSlider.Value, SatSlider.Value / 100, ValSlider.Value / 100);
            HexBox.Text = ColorUtil.ToHex(c);
            Preview.Background = new SolidColorBrush(c);
            Hex = ColorUtil.ToHex(c);
            _updating = false;
        }
    }
}
