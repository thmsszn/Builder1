using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GlassTaskbarStudio.UI
{
    /// <summary>Draws the app icon (a glossy glass pill on a gradient tile) at runtime - used for the window and the tray.</summary>
    internal static class IconFactory
    {
        public static BitmapSource CreateBitmap(int size)
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                double s = size;
                var tile = new Rect(s * 0.04, s * 0.04, s * 0.92, s * 0.92);
                dc.DrawRoundedRectangle(new LinearGradientBrush(Color.FromRgb(0x8B, 0x7C, 0xFF), Color.FromRgb(0x3F, 0xB0, 0xFF), 45), null, tile, s * 0.22, s * 0.22);
                var pill = new Rect(s * 0.17, s * 0.40, s * 0.66, s * 0.22);
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(70, 255, 255, 255)), new Pen(new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)), s * 0.015), pill, s * 0.11, s * 0.11);
                var gloss = new Rect(pill.X + s * 0.01, pill.Y + s * 0.01, pill.Width - s * 0.02, pill.Height * 0.45);
                dc.DrawRoundedRectangle(new LinearGradientBrush(Color.FromArgb(170, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90), null, gloss, s * 0.09, s * 0.09);
                dc.DrawEllipse(Brushes.White, null, new Point(s * 0.30, s * 0.51), s * 0.035, s * 0.035);
                dc.DrawEllipse(Brushes.White, null, new Point(s * 0.50, s * 0.51), s * 0.035, s * 0.035);
                dc.DrawEllipse(Brushes.White, null, new Point(s * 0.70, s * 0.51), s * 0.035, s * 0.035);
            }
            var bmp = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(visual);
            bmp.Freeze();
            return bmp;
        }

        public static System.Drawing.Icon CreateTrayIcon()
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(CreateBitmap(32)));
            using (var ms = new MemoryStream())
            {
                encoder.Save(ms);
                ms.Position = 0;
                using (var bmp = new System.Drawing.Bitmap(ms))
                    return System.Drawing.Icon.FromHandle(bmp.GetHicon());
            }
        }
    }
}
