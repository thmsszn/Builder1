using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;

namespace GlassTaskbarStudio.Core
{
    /// <summary>
    /// Finds where the centered app icons really are, using UI Automation (the same accessibility tree screen readers use).
    /// Locale-independent: it groups the buttons nearest the center that sit close together, which excludes
    /// the Widgets button on the far left and the system tray on the right.
    /// </summary>
    internal static class TaskbarMeasure
    {
        /// <summary>Returns the bounding box of the app icon group in screen pixels, or null if it cannot be determined.</summary>
        public static Rect? MeasureAppGroup(IntPtr taskbar, Native.RECT bar, double trayLeftScreen, bool centered, double dpi)
        {
            try
            {
                var root = AutomationElement.FromHandle(taskbar);
                var buttons = root.FindAll(TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button));

                var rects = new List<Rect>();
                foreach (AutomationElement e in buttons)
                {
                    Rect r;
                    try { r = e.Current.BoundingRectangle; } catch { continue; }
                    if (r.IsEmpty || r.Width < 16 * dpi || r.Height < 16 * dpi || r.Width > 400 * dpi) continue;
                    double cy = r.Y + r.Height / 2;
                    if (cy < bar.Top || cy > bar.Bottom) continue;
                    if (r.Left >= trayLeftScreen) continue;               // belongs to the tray
                    rects.Add(r);
                }
                if (rects.Count == 0) return null;

                rects.Sort((a, b) => a.Left.CompareTo(b.Left));
                double center = bar.Left + bar.Width / 2.0;
                int seed = centered
                    ? rects.Select((r, i) => (i, d: Math.Abs(r.X + r.Width / 2 - center))).OrderBy(t => t.d).First().i
                    : 0;

                double maxGap = 40 * dpi;
                int lo = seed, hi = seed;
                double right = rects[seed].Right, left = rects[seed].Left;
                for (int i = seed + 1; i < rects.Count; i++)
                {
                    if (rects[i].Left - right > maxGap) break;
                    right = Math.Max(right, rects[i].Right); hi = i;
                }
                for (int i = seed - 1; i >= 0; i--)
                {
                    if (left - rects[i].Right > maxGap) break;
                    left = Math.Min(left, rects[i].Left); lo = i;
                }
                return new Rect(left, bar.Top, right - left, bar.Height);
            }
            catch { return null; }
        }

        /// <summary>Width of the notification area / clock block in pixels.</summary>
        public static bool TryGetTray(IntPtr taskbar, out Native.RECT tray)
        {
            tray = default;
            var h = Native.FindWindowEx(taskbar, IntPtr.Zero, "TrayNotifyWnd", null);
            return h != IntPtr.Zero && Native.GetWindowRect(h, out tray) && tray.Width > 20;
        }
    }
}
