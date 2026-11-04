using System;
using System.Collections.Generic;
using System.Windows;

namespace GlassTaskbarStudio.Core
{
    public sealed class PillLayout
    {
        public List<Rect> Pills { get; } = new List<Rect>();
        public double Radius { get; set; }
        public string Key => $"{Radius:0.#}|" + string.Join(";", Pills.ConvertAll(p => $"{p.X:0.#},{p.Y:0.#},{p.Width:0.#},{p.Height:0.#}"));
    }

    /// <summary>
    /// Pure geometry: where the visible glass "pills" of the taskbar are. Used by the real engine (device pixels)
    /// and the live preview (scaled), so both always agree.
    /// </summary>
    public static class LayoutCalculator
    {
        public static PillLayout Compute(TaskbarSettings s, double barW, double barH, double scale,
                                         double trayW, Rect? appGroup, bool compactSupported)
        {
            double gap = s.Floating ? Math.Min(s.FloatingGap * scale, barH * 0.22) : 0;   // never eat into the icons
            double side = s.Floating ? s.SideGap * scale : 0;
            double top = gap, h = barH - 2 * gap;
            var layout = new PillLayout { Radius = Math.Max(0, Math.Min(s.CornerRadius * scale, h / 2)) };

            if (!(s.Compact && compactSupported))
            {
                layout.Pills.Add(new Rect(side, top, Math.Max(1, barW - 2 * side), h));
                return layout;
            }

            // Compact: one pill around the app icons + one around the system tray ("islands").
            double tw = Math.Max(60 * scale, trayW);
            var tray = new Rect(barW - side - tw, top, tw, h);

            Rect apps;
            if (s.AutoWidth && appGroup.HasValue && appGroup.Value.Width > 20)
            {
                double pad = 14 * scale;
                apps = new Rect(appGroup.Value.X - pad, top, appGroup.Value.Width + 2 * pad, h);
            }
            else
            {
                double w = barW * s.WidthPercent / 100.0;
                double x = s.Alignment == IconAlignment.Center ? (barW - w) / 2 : side;
                apps = new Rect(x, top, w, h);
            }

            double maxRight = tray.X - 8 * scale;
            double left = Math.Max(side, apps.X), right = Math.Min(maxRight, apps.Right);
            if (right - left < 96 * scale) right = Math.Min(maxRight, left + 96 * scale);
            layout.Pills.Add(new Rect(left, top, Math.Max(1, right - left), h));
            layout.Pills.Add(tray);
            return layout;
        }
    }
}
