using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Printing;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace ProjectZ
{
    [SupportedOSPlatform("windows")]
    public sealed class WindowsPhotoPrintService : IPhotoPrintService
    {
        public bool IsSupported => true;

        public IReadOnlyList<string> GetPrinters()
        {
            var printers = new List<string>();
            foreach (string name in PrinterSettings.InstalledPrinters)
                printers.Add(name);
            return printers;
        }

        public string DefaultPrinter
        {
            get
            {
                // A default-constructed PrinterSettings reports the system default.
                var settings = new PrinterSettings();
                return settings.IsValid ? settings.PrinterName : null;
            }
        }

        public void Print(string printerName, PhotoPrintJob job, Action<bool, string> onComplete)
        {
            // Print() blocks while the job spools, which can take several seconds.
            Task.Run(() =>
            {
                try
                {
                    using var bitmap = CreateBitmap(job);
                    using var document = new PrintDocument();

                    document.DocumentName = job.JobName ?? "Photo";

                    if (!string.IsNullOrEmpty(printerName))
                        document.PrinterSettings.PrinterName = printerName;

                    if (!document.PrinterSettings.IsValid)
                    {
                        Debug.WriteLine($"[Print] Invalid printer '{printerName}'.");
                        onComplete?.Invoke(false, "That printer is no longer available.");
                        return;
                    }

                    document.PrinterSettings.Copies = (short)Math.Clamp(job.Copies, 1, 99);
                    document.DefaultPageSettings.Color = !job.Grayscale;
                    document.OriginAtMargins = false;

                    var page = job;
                    document.PrintPage += (sender, e) => DrawPage(e, bitmap, page);
                    document.Print();

                    Debug.WriteLine($"[Print] Sent '{document.DocumentName}' to '{document.PrinterSettings.PrinterName}'.");
                    onComplete?.Invoke(true, null);
                }
                catch (Exception exception)
                {
                    Debug.WriteLine($"[Print] Failed: {exception}");
                    onComplete?.Invoke(false, exception.Message);
                }
            });
        }

        private static Bitmap CreateBitmap(PhotoPrintJob job)
        {
            var bitmap = new Bitmap(job.Width, job.Height, PixelFormat.Format32bppArgb);
            var data = bitmap.LockBits(
                new Rectangle(0, 0, job.Width, job.Height),
                ImageLockMode.WriteOnly,
                PixelFormat.Format32bppArgb);

            var row = new byte[job.Width * 4];
            for (var y = 0; y < job.Height; y++)
            {
                var source = y * job.Width * 4;
                for (var x = 0; x < job.Width; x++)
                {
                    // MonoGame Color is R,G,B,A. GDI+ 32bppArgb is B,G,R,A in memory.
                    var r = job.Rgba[source + x * 4 + 0];
                    var g = job.Rgba[source + x * 4 + 1];
                    var b = job.Rgba[source + x * 4 + 2];

                    if (job.Grayscale)
                    {
                        var luminance = (byte)((r * 77 + g * 151 + b * 28) >> 8);
                        r = g = b = luminance;
                    }

                    row[x * 4 + 0] = b;
                    row[x * 4 + 1] = g;
                    row[x * 4 + 2] = r;
                    // Printers have no alpha; photos are opaque, so force it.
                    row[x * 4 + 3] = 255;
                }

                Marshal.Copy(row, 0, IntPtr.Add(data.Scan0, y * data.Stride), row.Length);
            }

            bitmap.UnlockBits(data);
            return bitmap;
        }

        private static void DrawPage(PrintPageEventArgs e, Bitmap bitmap, PhotoPrintJob job)
        {
            var graphics = e.Graphics;
            graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            graphics.PixelOffsetMode = PixelOffsetMode.Half;
            graphics.SmoothingMode = SmoothingMode.None;
            graphics.CompositingQuality = CompositingQuality.HighSpeed;

            // Printer page units are hundredths of an inch.
            var bounds = e.MarginBounds;
            var targetWidth = Math.Min(bounds.Width, (int)(job.WidthInches * 100f));
            var targetHeight = targetWidth * bitmap.Height / bitmap.Width;

            if (targetHeight > bounds.Height)
            {
                targetHeight = bounds.Height;
                targetWidth = targetHeight * bitmap.Width / bitmap.Height;
            }

            var destination = new Rectangle(
                bounds.X + (bounds.Width - targetWidth) / 2,
                bounds.Y + (bounds.Height - targetHeight) / 2,
                targetWidth,
                targetHeight);

            // TileFlipXY stops GDI+ sampling past the edges and fringing the border.
            using var attributes = new ImageAttributes();
            attributes.SetWrapMode(WrapMode.TileFlipXY);

            graphics.DrawImage(bitmap, destination, 0, 0, bitmap.Width, bitmap.Height,
                               GraphicsUnit.Pixel, attributes);
        }
    }
}
