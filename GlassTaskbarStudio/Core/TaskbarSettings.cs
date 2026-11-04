using System;
using System.Linq;
using System.Reflection;
using System.Windows.Media;

namespace GlassTaskbarStudio.Core
{
    public enum TaskbarMaterial { Clear, Blur, Acrylic, Opaque }
    public enum IconAlignment { Left, Center }
    public enum TaskbarSize { Small, Medium, Large }

    /// <summary>
    /// Every user-tweakable taskbar parameter. One instance is edited live in the UI (the "draft"),
    /// another one is what the engine currently renders. Defaults == the "Liquid Glass" preset.
    /// </summary>
    public sealed class TaskbarSettings : ObservableObject
    {
        TaskbarMaterial _material = TaskbarMaterial.Acrylic;
        string _tintHex = "#0A1226";
        double _transparency = 78, _gloss = 70, _radius = 24, _gap = 8, _sideGap = 12, _widthPercent = 55;
        double _shadow = 55, _borderOpacity = 60, _borderThickness = 1, _transitionMs = 350;
        string _borderHex = "#FFFFFF", _shadowHex = "#000000";
        bool _floating = true, _compact = true, _autoWidth = true, _shimmer, _animate = true, _allMonitors = true;
        IconAlignment _align = IconAlignment.Center;
        TaskbarSize _size = TaskbarSize.Medium;

        // ---- Transparency & blur (native accent policy) ----
        public TaskbarMaterial Material { get => _material; set => Set(ref _material, value); }
        /// <summary>0 = fully tinted, 100 = no tint (glass only).</summary>
        public double Transparency { get => _transparency; set => Set(ref _transparency, Clamp(value, 0, 100)); }

        // ---- Colors ----
        public string TintHex { get => _tintHex; set => Set(ref _tintHex, value); }
        public string BorderHex { get => _borderHex; set => Set(ref _borderHex, value); }
        public string ShadowHex { get => _shadowHex; set => Set(ref _shadowHex, value); }

        // ---- Appearance (overlay + region) ----
        public double GlossIntensity { get => _gloss; set => Set(ref _gloss, Clamp(value, 0, 100)); }
        public double CornerRadius { get => _radius; set => Set(ref _radius, Clamp(value, 0, 36)); }
        public double ShadowIntensity { get => _shadow; set => Set(ref _shadow, Clamp(value, 0, 100)); }
        public double BorderOpacity { get => _borderOpacity; set => Set(ref _borderOpacity, Clamp(value, 0, 100)); }
        public double BorderThickness { get => _borderThickness; set => Set(ref _borderThickness, Clamp(value, 0, 3)); }
        public TaskbarSize Height { get => _size; set => Set(ref _size, value); }

        // ---- Position & layout ----
        public IconAlignment Alignment { get => _align; set => Set(ref _align, value); }
        public bool Floating { get => _floating; set => Set(ref _floating, value); }
        public double FloatingGap { get => _gap; set => Set(ref _gap, Clamp(value, 0, 12)); }
        public double SideGap { get => _sideGap; set => Set(ref _sideGap, Clamp(value, 0, 80)); }
        public bool Compact { get => _compact; set => Set(ref _compact, value); }
        public bool AutoWidth { get => _autoWidth; set => Set(ref _autoWidth, value); }
        public double WidthPercent { get => _widthPercent; set => Set(ref _widthPercent, Clamp(value, 30, 100)); }
        public bool AllMonitors { get => _allMonitors; set => Set(ref _allMonitors, value); }

        // ---- Animations ----
        public bool AnimateTransitions { get => _animate; set => Set(ref _animate, value); }
        public double TransitionMs { get => _transitionMs; set => Set(ref _transitionMs, Clamp(value, 100, 1000)); }
        public bool Shimmer { get => _shimmer; set => Set(ref _shimmer, value); }

        static double Clamp(double v, double lo, double hi) => double.IsNaN(v) ? lo : Math.Max(lo, Math.Min(hi, v));

        // ---------- generic helpers (reflection over the property list; settings are tiny and rarely copied) ----------
        static readonly PropertyInfo[] Props = typeof(TaskbarSettings)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p => p.CanRead && p.CanWrite && p.DeclaringType == typeof(TaskbarSettings)).ToArray();

        public TaskbarSettings Clone() { var c = new TaskbarSettings(); c.CopyFrom(this); return c; }

        public void CopyFrom(TaskbarSettings src)
        {
            foreach (var p in Props) p.SetValue(this, p.GetValue(src));
        }

        public bool ValueEquals(TaskbarSettings o) =>
            o != null && Props.All(p => Equals(p.GetValue(this), p.GetValue(o)));

        /// <summary>Linear interpolation of numbers and colors; enums/bools snap to the target immediately.</summary>
        public static TaskbarSettings Lerp(TaskbarSettings a, TaskbarSettings b, double t)
        {
            var r = b.Clone();
            foreach (var p in Props)
            {
                if (p.PropertyType == typeof(double))
                    p.SetValue(r, (double)p.GetValue(a) + ((double)p.GetValue(b) - (double)p.GetValue(a)) * t);
                else if (p.PropertyType == typeof(string))
                    p.SetValue(r, ColorUtil.ToHex(ColorUtil.Lerp(ColorUtil.Parse((string)p.GetValue(a)), ColorUtil.Parse((string)p.GetValue(b)), t)));
            }
            return r;
        }
    }

    public static class ColorUtil
    {
        public static Color Parse(string hex, Color? fallback = null)
        {
            try { return (Color)ColorConverter.ConvertFromString(hex); }
            catch { return fallback ?? Colors.White; }
        }

        public static bool IsValidHex(string s)
        {
            if (string.IsNullOrWhiteSpace(s) || s[0] != '#' || (s.Length != 7 && s.Length != 9)) return false;
            return s.Skip(1).All(Uri.IsHexDigit);
        }

        public static string ToHex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

        public static Color Lerp(Color a, Color b, double t) => Color.FromRgb(
            (byte)Math.Round(a.R + (b.R - a.R) * t), (byte)Math.Round(a.G + (b.G - a.G) * t), (byte)Math.Round(a.B + (b.B - a.B) * t));

        public static Color WithAlpha(Color c, double a01) =>
            Color.FromArgb((byte)Math.Round(Math.Max(0, Math.Min(1, a01)) * 255), c.R, c.G, c.B);

        public static void ToHsv(Color c, out double h, out double s, out double v)
        {
            double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
            double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), d = max - min;
            h = d == 0 ? 0 : max == r ? 60 * (((g - b) / d) % 6) : max == g ? 60 * ((b - r) / d + 2) : 60 * ((r - g) / d + 4);
            if (h < 0) h += 360;
            s = max == 0 ? 0 : d / max; v = max;
        }

        public static Color FromHsv(double h, double s, double v)
        {
            double c = v * s, x = c * (1 - Math.Abs((h / 60) % 2 - 1)), m = v - c, r, g, b;
            if (h < 60) { r = c; g = x; b = 0; } else if (h < 120) { r = x; g = c; b = 0; }
            else if (h < 180) { r = 0; g = c; b = x; } else if (h < 240) { r = 0; g = x; b = c; }
            else if (h < 300) { r = x; g = 0; b = c; } else { r = c; g = 0; b = x; }
            return Color.FromRgb((byte)Math.Round((r + m) * 255), (byte)Math.Round((g + m) * 255), (byte)Math.Round((b + m) * 255));
        }
    }
}
