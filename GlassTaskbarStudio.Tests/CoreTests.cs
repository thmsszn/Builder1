using System;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using GlassTaskbarStudio.Core;
using Xunit;

namespace GlassTaskbarStudio.Tests
{
    public class PresetTests
    {
        [Fact]
        public void HasTheSixRequestedPresets()
        {
            var names = Presets.All.Select(p => p.Name).ToArray();
            Assert.Equal(new[] { "Liquid Glass", "Windows 11 Pro", "Midnight", "Frosted Glass", "Minimal", "Neon" }, names);
        }

        [Fact]
        public void DefaultIsLiquidGlassAndEqualsFreshSettings()
        {
            Assert.Equal("Liquid Glass", Presets.Default.Name);
            Assert.True(Presets.Default.Settings.ValueEquals(new TaskbarSettings()));
        }

        [Fact]
        public void EveryPresetIsDistinctAndMatchable()
        {
            foreach (var p in Presets.All)
                Assert.Same(p, Presets.Match(p.Settings.Clone()));
            Assert.Equal(Presets.All.Count, Presets.All.Select(p => p.Name).Distinct().Count());
            for (int i = 0; i < Presets.All.Count; i++)
                for (int j = i + 1; j < Presets.All.Count; j++)
                    Assert.False(Presets.All[i].Settings.ValueEquals(Presets.All[j].Settings));
        }

        [Fact]
        public void PresetColorsAreValidHex()
        {
            foreach (var p in Presets.All)
            {
                Assert.True(ColorUtil.IsValidHex(p.Settings.TintHex), p.Name);
                Assert.True(ColorUtil.IsValidHex(p.Settings.BorderHex), p.Name);
                Assert.True(ColorUtil.IsValidHex(p.Settings.ShadowHex), p.Name);
            }
        }

        [Fact]
        public void MinimalPresetIsFullyClearAndFlush()
        {
            var m = Presets.All.Single(p => p.Name == "Minimal").Settings;
            Assert.Equal(TaskbarMaterial.Clear, m.Material);
            Assert.False(m.Floating); Assert.False(m.Compact);
            Assert.Equal(0, m.CornerRadius);
        }
    }

    public class SettingsTests
    {
        [Fact]
        public void ValuesAreClamped()
        {
            var s = new TaskbarSettings { Transparency = 500, CornerRadius = -3, FloatingGap = 99, WidthPercent = 1, TransitionMs = 5 };
            Assert.Equal(100, s.Transparency); Assert.Equal(0, s.CornerRadius);
            Assert.Equal(12, s.FloatingGap); Assert.Equal(30, s.WidthPercent); Assert.Equal(100, s.TransitionMs);
            s.CornerRadius = double.NaN;
            Assert.Equal(0, s.CornerRadius);
        }

        [Fact]
        public void CloneIsIndependent()
        {
            var a = new TaskbarSettings { CornerRadius = 10 };
            var b = a.Clone();
            Assert.True(a.ValueEquals(b));
            b.CornerRadius = 20;
            Assert.False(a.ValueEquals(b));
            Assert.Equal(10, a.CornerRadius);
        }

        [Fact]
        public void CopyFromRaisesPropertyChangedOnlyForChanges()
        {
            var target = new TaskbarSettings();
            int raised = 0;
            target.PropertyChanged += (s, e) => raised++;
            target.CopyFrom(new TaskbarSettings()); // identical -> nothing changes
            Assert.Equal(0, raised);
            target.CopyFrom(Presets.All.Single(p => p.Name == "Neon").Settings);
            Assert.True(raised > 0);
        }

        [Fact]
        public void LerpInterpolatesNumbersAndColorsAndSnapsEnums()
        {
            var a = new TaskbarSettings { CornerRadius = 0, TintHex = "#000000", Material = TaskbarMaterial.Blur };
            var b = new TaskbarSettings { CornerRadius = 24, TintHex = "#FFFFFF", Material = TaskbarMaterial.Acrylic };
            var mid = TaskbarSettings.Lerp(a, b, 0.5);
            Assert.Equal(12, mid.CornerRadius, 3);
            Assert.Equal("#808080", mid.TintHex);
            Assert.Equal(TaskbarMaterial.Acrylic, mid.Material);
            Assert.True(TaskbarSettings.Lerp(a, b, 1).ValueEquals(b));
        }

        [Fact]
        public void JsonRoundTripKeepsEverythingAndWritesEnumNames()
        {
            var cfg = new AppConfig { Applied = Presets.All.Single(p => p.Name == "Neon").Settings.Clone(), StartWithWindows = false, RefreshMs = 2500 };
            cfg.Backup.Captured = true; cfg.Backup.TaskbarAl = 0;
            string json = JsonSerializer.Serialize(cfg, AppConfig.JsonOptions);
            Assert.Contains("\"Acrylic\"", json);
            var back = JsonSerializer.Deserialize<AppConfig>(json, AppConfig.JsonOptions);
            Assert.True(cfg.Applied.ValueEquals(back.Applied));
            Assert.False(back.StartWithWindows);
            Assert.Equal(2500, back.RefreshMs);
            Assert.True(back.Backup.Captured);
            Assert.Equal(0, back.Backup.TaskbarAl);
            Assert.Null(back.Backup.TaskbarSi);
        }
    }

    public class LayoutTests
    {
        const double W = 1920, H = 48;

        [Fact]
        public void FlushFullWidthWhenNotFloating()
        {
            var s = new TaskbarSettings { Floating = false, Compact = false, CornerRadius = 0 };
            var l = LayoutCalculator.Compute(s, W, H, 1, 220, null, true);
            var p = Assert.Single(l.Pills);
            Assert.Equal(new Rect(0, 0, W, H), p);
        }

        [Fact]
        public void FloatingInsetsAndNeverEatsTheIcons()
        {
            var s = new TaskbarSettings { Floating = true, FloatingGap = 12, SideGap = 30, Compact = false, CornerRadius = 36 };
            var l = LayoutCalculator.Compute(s, W, H, 1, 220, null, true);
            var p = l.Pills[0];
            Assert.Equal(30, p.X); Assert.Equal(W - 60, p.Width);
            Assert.True(p.Y <= H * 0.22 + 1e-9);                  // gap is capped at 22% of the bar
            Assert.True(p.Height >= H * 0.55);                    // at least half the bar stays visible
            Assert.True(l.Radius <= p.Height / 2 + 1e-9);         // radius never exceeds a full pill
        }

        [Fact]
        public void CompactBuildsAppPillAndTrayPillWithoutOverlap()
        {
            var s = new TaskbarSettings { Compact = true, AutoWidth = true, Floating = true, SideGap = 12 };
            var apps = new Rect(800, 0, 320, H);
            var l = LayoutCalculator.Compute(s, W, H, 1, 220, apps, true);
            Assert.Equal(2, l.Pills.Count);
            var a = l.Pills[0]; var t = l.Pills[1];
            Assert.True(a.X <= 800 && a.Right >= 1120);            // contains the measured icons
            Assert.True(a.Right < t.X);                           // islands do not touch
            Assert.Equal(W - 12, t.Right, 3);                     // tray pill ends at the (floating) edge
            Assert.Equal(220, t.Width, 3);
        }

        [Fact]
        public void CompactFixedWidthIsCenteredWhenIconsAreCentered()
        {
            var s = new TaskbarSettings { Compact = true, AutoWidth = false, WidthPercent = 50, Alignment = IconAlignment.Center };
            var l = LayoutCalculator.Compute(s, W, H, 1, 220, null, true);
            var a = l.Pills[0];
            Assert.Equal(W / 2, a.X + a.Width / 2, 1);
            Assert.Equal(W * 0.5, a.Width, 1);
        }

        [Fact]
        public void CompactIsIgnoredWhereUnsupported()
        {
            var s = new TaskbarSettings { Compact = true };
            Assert.Single(LayoutCalculator.Compute(s, W, H, 1, 220, null, false).Pills);
        }

        [Fact]
        public void GeometryScalesWithDpi()
        {
            var s = new TaskbarSettings { Floating = true, FloatingGap = 8, SideGap = 12, Compact = false, CornerRadius = 20 };
            var one = LayoutCalculator.Compute(s, W, 48, 1, 220, null, true);
            var two = LayoutCalculator.Compute(s, W * 2, 96, 2, 440, null, true);
            Assert.Equal(one.Pills[0].X * 2, two.Pills[0].X, 3);
            Assert.Equal(one.Pills[0].Y * 2, two.Pills[0].Y, 3);
            Assert.Equal(one.Radius * 2, two.Radius, 3);
        }

        [Fact]
        public void KeyChangesWhenGeometryChanges()
        {
            var s = new TaskbarSettings();
            var a = LayoutCalculator.Compute(s, W, H, 1, 220, null, true).Key;
            s.CornerRadius = 10;
            Assert.NotEqual(a, LayoutCalculator.Compute(s, W, H, 1, 220, null, true).Key);
        }
    }

    public class ColorTests
    {
        [Theory]
        [InlineData("#FFFFFF", true)] [InlineData("#0a1226", true)] [InlineData("#80FFFFFF", true)]
        [InlineData("FFFFFF", false)] [InlineData("#GGGGGG", false)] [InlineData("#FFF", false)] [InlineData("", false)] [InlineData(null, false)]
        public void HexValidation(string hex, bool expected) => Assert.Equal(expected, ColorUtil.IsValidHex(hex));

        [Fact]
        public void HsvRoundTrip()
        {
            foreach (var hex in new[] { "#FF0000", "#00FF00", "#0000FF", "#8B7CFF", "#0A1226", "#FFFFFF", "#000000" })
            {
                var c = ColorUtil.Parse(hex);
                ColorUtil.ToHsv(c, out double h, out double s, out double v);
                var back = ColorUtil.FromHsv(h, s, v);
                Assert.InRange(Math.Abs(c.R - back.R), 0, 1); Assert.InRange(Math.Abs(c.G - back.G), 0, 1); Assert.InRange(Math.Abs(c.B - back.B), 0, 1);
            }
        }

        [Fact]
        public void InvalidColorFallsBack()
        {
            Assert.Equal(Colors.White, ColorUtil.Parse("not a color"));
            Assert.Equal(Colors.Black, ColorUtil.Parse("nope", Colors.Black));
        }
    }
}
