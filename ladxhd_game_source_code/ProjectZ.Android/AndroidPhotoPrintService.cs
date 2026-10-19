using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Microsoft.Win32.SafeHandles;
using Android.Graphics;
using Android.OS;
using Android.Print;
using Android.Print.Pdf;

namespace ProjectZ.Android
{
    internal sealed class AndroidPhotoPrintService : IPhotoPrintService
    {
        private const string SystemPrintServiceName = "Android Print Service";

        private readonly MainActivity _activity;

        public AndroidPhotoPrintService(MainActivity activity)
        {
            _activity = activity;
        }

        public bool IsSupported => _activity?.GetSystemService(global::Android.Content.Context.PrintService) is PrintManager;

        // Android's system print sheet selects the destination instead of exposing a default queue.
        public IReadOnlyList<string> GetPrinters() => IsSupported ? [SystemPrintServiceName] : [];

        public string DefaultPrinter => IsSupported ? SystemPrintServiceName : null;

        public void Print(string printerName, PhotoPrintJob job, Action<bool, string> onComplete)
        {
            if (!IsSupported)
            {
                onComplete?.Invoke(false, "Android printing is unavailable.");
                return;
            }

            _activity.RunOnUiThread(() =>
            {
                try
                {
                    var printManager = (PrintManager)_activity.GetSystemService(global::Android.Content.Context.PrintService);
                    var attributes = new PrintAttributes.Builder().Build();
                    printManager.Print(string.IsNullOrWhiteSpace(job.JobName) ? "Photo" : job.JobName,
                        new PhotoDocumentAdapter(_activity, job, onComplete), attributes);
                }
                catch (Exception exception)
                {
                    onComplete?.Invoke(false, exception.Message);
                }
            });
        }

        private sealed class PhotoDocumentAdapter : PrintDocumentAdapter
        {
            private readonly MainActivity _activity;
            private readonly PhotoPrintJob _job;
            private readonly Action<bool, string> _onComplete;
            private int _completed;
            private PrintAttributes _attributes;

            public PhotoDocumentAdapter(MainActivity activity, PhotoPrintJob job, Action<bool, string> onComplete)
            {
                _activity = activity;
                _job = job;
                _onComplete = onComplete;
            }

            public override void OnLayout(PrintAttributes oldAttributes, PrintAttributes newAttributes,
                CancellationSignal cancellationSignal, LayoutResultCallback callback, Bundle extras)
            {
                _attributes = newAttributes;
                if (cancellationSignal.IsCanceled)
                {
                    callback.OnLayoutCancelled();
                    Complete(false, "Printing was cancelled.");
                    return;
                }

                var info = new PrintDocumentInfo.Builder(string.IsNullOrWhiteSpace(_job.JobName) ? "Photo" : _job.JobName)
                    .SetContentType(PrintContentType.Photo)
                    .SetPageCount(1)
                    .Build();
                callback.OnLayoutFinished(info, !newAttributes.Equals(oldAttributes));
            }

            public override void OnWrite(PageRange[] pages, ParcelFileDescriptor destination,
                CancellationSignal cancellationSignal, WriteResultCallback callback)
            {
                try
                {
                    if (cancellationSignal.IsCanceled)
                    {
                        callback.OnWriteCancelled();
                        Complete(false, "Printing was cancelled.");
                        return;
                    }

                    if (_attributes == null)
                        throw new InvalidOperationException("The print layout is unavailable.");

                    using var document = new PrintedPdfDocument(_activity, _attributes);
                    using var bitmap = CreateBitmap(_job);
                    var page = document.StartPage(0);
                    DrawPhoto(page.Canvas, page.Info.ContentRect, bitmap, _job);
                    document.FinishPage(page);

                    using var output = new FileStream(
                        new SafeFileHandle((IntPtr)destination.DetachFd(), ownsHandle: true), FileAccess.Write);
                    document.WriteTo(output);
                    callback.OnWriteFinished([new PageRange(0, 0)]);
                    Complete(true, null);
                }
                catch (Exception exception)
                {
                    callback.OnWriteFailed(exception.Message);
                    Complete(false, exception.Message);
                }
            }

            public override void OnFinish()
            {
                Complete(false, "Printing was cancelled.");
            }

            private void Complete(bool success, string error)
            {
                if (Interlocked.Exchange(ref _completed, 1) == 0)
                    _onComplete?.Invoke(success, error);
            }

            private static Bitmap CreateBitmap(PhotoPrintJob job)
            {
                if (job.Width <= 0 || job.Height <= 0 || job.Rgba == null || job.Rgba.Length < checked(job.Width * job.Height * 4))
                    throw new ArgumentException("The photo data is invalid.", nameof(job));

                var pixels = new int[checked(job.Width * job.Height)];
                for (var source = 0; source < pixels.Length; source++)
                {
                    var offset = source * 4;
                    var red = job.Rgba[offset];
                    var green = job.Rgba[offset + 1];
                    var blue = job.Rgba[offset + 2];
                    if (job.Grayscale)
                        red = green = blue = (byte)((red * 77 + green * 151 + blue * 28) >> 8);

                    pixels[source] = (job.Rgba[offset + 3] << 24) | (red << 16) | (green << 8) | blue;
                }

                var bitmap = Bitmap.CreateBitmap(job.Width, job.Height, Bitmap.Config.Argb8888);
                bitmap.SetPixels(pixels, 0, job.Width, 0, 0, job.Width, job.Height);
                return bitmap;
            }

            private static void DrawPhoto(Canvas canvas, Rect contentRect, Bitmap bitmap, PhotoPrintJob job)
            {
                // PrintedPdfDocument uses PDF points, not the printer's raster resolution.
                var width = Math.Min(Math.Max(job.WidthInches, 0.1f) * 72f, contentRect.Width());
                var height = width * job.Height / job.Width;
                if (height > contentRect.Height())
                {
                    height = contentRect.Height();
                    width = height * job.Width / job.Height;
                }

                var left = contentRect.Left + (contentRect.Width() - width) / 2f;
                var top = contentRect.Top + (contentRect.Height() - height) / 2f;
                var destination = new RectF(left, top, left + width, top + height);
                using var paint = new Paint(PaintFlags.FilterBitmap | PaintFlags.Dither);
                canvas.DrawBitmap(bitmap, null, destination, paint);
            }
        }
    }
}
