using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using GlassTaskbarStudio.Core;

namespace GlassTaskbarStudio.UI.Controls
{
    /// <summary>
    /// Draws the glass "pills" of the taskbar: shadow, (preview only) frosted backdrop + tint, gloss, rim light, border, shimmer.
    /// The very same code renders the on-desktop overlay and the in-app live preview, so they always match.
    /// Coordinates are in whatever unit the caller uses (device pixels for the overlay, scaled units for the preview).
    /// </summary>
    public sealed class GlassPillVisual : Canvas
    {
        public void Render(Size size, IList<Rect> pills, double radius, TaskbarSettings s, double scale,
                           ImageSource backdrop, Rect backdropRect, bool animate)
        {
            Children.Clear();
            Width = size.Width; Height = size.Height;
            if (pills.Count == 0) return;

            var geo = new GeometryGroup { FillRule = FillRule.Nonzero };
            foreach (var p in pills) geo.Children.Add(new RectangleGeometry(p, radius, radius));
            geo.Freeze();

            double gloss = s.GlossIntensity / 100.0;
            AddShadow(s, geo, size, scale);
            if (backdrop != null) AddBackdrop(s, geo, backdrop, backdropRect, scale);

            foreach (var p in pills)
            {
                AddGloss(p, radius, gloss);
                AddBorders(p, radius, s, scale);
            }
            if (s.Shimmer && animate && gloss > 0) AddShimmer(geo, size);
        }

        // ---- shadow / glow: a blurred copy of the pills, clipped so it only shows OUTSIDE the glass ----
        void AddShadow(TaskbarSettings s, Geometry pills, Size size, double scale)
        {
            double k = s.ShadowIntensity / 100.0;
            if (k <= 0.01) return;
            var shifted = pills.Clone();
            shifted.Transform = new TranslateTransform(0, (2 + 4 * k) * scale);
            var path = new Path
            {
                Data = shifted,
                Fill = new SolidColorBrush(ColorUtil.Parse(s.ShadowHex, Colors.Black)),
                Opacity = 0.2 + 0.7 * k,
                Effect = new BlurEffect { Radius = (5 + 16 * k) * scale, KernelType = KernelType.Gaussian, RenderingBias = RenderingBias.Performance },
                Clip = new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(new Rect(0, 0, size.Width, size.Height)), pills),
                IsHitTestVisible = false
            };
            Children.Add(path);
        }

        // ---- preview only: simulate what the native accent policy does behind the real taskbar ----
        void AddBackdrop(TaskbarSettings s, Geometry pills, ImageSource backdrop, Rect rect, double scale)
        {
            if (s.Material != TaskbarMaterial.Clear)
            {
                double blur = s.Material == TaskbarMaterial.Blur ? 10 : s.Material == TaskbarMaterial.Acrylic ? 22 : 0;
                var img = new Image
                {
                    Source = backdrop, Width = rect.Width, Height = rect.Height, Stretch = Stretch.Fill, Clip = pills, IsHitTestVisible = false
                };
                if (blur > 0) img.Effect = new BlurEffect { Radius = blur * scale, KernelType = KernelType.Gaussian, RenderingBias = RenderingBias.Quality };
                SetLeft(img, rect.X); SetTop(img, rect.Y);
                Children.Add(img);
            }

            double alpha = s.Material == TaskbarMaterial.Clear ? 0 : s.Material == TaskbarMaterial.Opaque ? 1 : (100 - s.Transparency) / 100.0;
            if (alpha > 0)
                Children.Add(new Path { Data = pills, Fill = new SolidColorBrush(ColorUtil.WithAlpha(ColorUtil.Parse(s.TintHex), alpha)), IsHitTestVisible = false });
        }

        // ---- glossy sheen: bright upper half, soft terminator, slightly darker lower rim ----
        void AddGloss(Rect p, double radius, double k)
        {
            if (k <= 0.01) return;
            var brush = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
            brush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(130 * k), 255, 255, 255), 0));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(34 * k), 255, 255, 255), 0.48));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(4 * k), 255, 255, 255), 0.52));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(60 * k), 0, 0, 0), 1));
            brush.Freeze();
            AddRect(p, radius, brush, null, 0);
        }

        // ---- crisp outer rim + softer inner rim light (the "liquid glass" edge) ----
        void AddBorders(Rect p, double radius, TaskbarSettings s, double scale)
        {
            double t = s.BorderThickness * scale;
            if (t < 0.1 || s.BorderOpacity <= 0) return;
            var c = ColorUtil.Parse(s.BorderHex);
            double a = s.BorderOpacity / 100.0;
            var outer = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
            outer.GradientStops.Add(new GradientStop(ColorUtil.WithAlpha(c, a), 0));
            outer.GradientStops.Add(new GradientStop(ColorUtil.WithAlpha(c, a * 0.35), 0.55));
            outer.GradientStops.Add(new GradientStop(ColorUtil.WithAlpha(c, a * 0.6), 1));
            outer.Freeze();
            var r = Rect.Inflate(p, -t / 2, -t / 2);
            AddRect(r, Math.Max(0, radius - t / 2), null, outer, t);

            var inner = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
            inner.GradientStops.Add(new GradientStop(ColorUtil.WithAlpha(Colors.White, a * 0.35), 0));
            inner.GradientStops.Add(new GradientStop(Colors.Transparent, 0.5));
            inner.Freeze();
            double off = t + 1.2 * scale;
            var ri = Rect.Inflate(p, -off, -off);
            if (ri.Width > 4 && ri.Height > 4) AddRect(ri, Math.Max(0, radius - off), null, inner, Math.Max(1, scale));
        }

        void AddRect(Rect r, double radius, Brush fill, Brush stroke, double thickness)
        {
            var rect = new Rectangle
            {
                Width = Math.Max(0, r.Width), Height = Math.Max(0, r.Height),
                RadiusX = radius, RadiusY = radius, Fill = fill, Stroke = stroke, StrokeThickness = thickness, IsHitTestVisible = false
            };
            SetLeft(rect, r.X); SetTop(rect, r.Y);
            Children.Add(rect);
        }

        // ---- optional light sweep that glides across the glass every few seconds ----
        void AddShimmer(Geometry pills, Size size)
        {
            var host = new Canvas { Width = size.Width, Height = size.Height, Clip = pills, IsHitTestVisible = false };
            double w = Math.Max(40, size.Width * 0.12);
            var brush = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
            brush.GradientStops.Add(new GradientStop(Colors.Transparent, 0));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(70, 255, 255, 255), 0.5));
            brush.GradientStops.Add(new GradientStop(Colors.Transparent, 1));
            var sweep = new Rectangle { Width = w, Height = size.Height, Fill = brush, RenderTransform = new TranslateTransform(-w, 0) };
            host.Children.Add(sweep);
            Children.Add(host);

            var anim = new DoubleAnimationUsingKeyFrames { RepeatBehavior = RepeatBehavior.Forever };
            anim.KeyFrames.Add(new LinearDoubleKeyFrame(-w, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            anim.KeyFrames.Add(new EasingDoubleKeyFrame(size.Width + w, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(1.8)), new SineEase { EasingMode = EasingMode.EaseInOut }));
            anim.KeyFrames.Add(new LinearDoubleKeyFrame(size.Width + w, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(8))));
            Timeline.SetDesiredFrameRate(anim, 30); // keep CPU low
            ((TranslateTransform)sweep.RenderTransform).BeginAnimation(TranslateTransform.XProperty, anim);
        }
    }
}
