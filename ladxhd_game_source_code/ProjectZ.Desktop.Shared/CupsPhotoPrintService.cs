using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;

namespace ProjectZ
{
    public sealed class CupsPhotoPrintService : IPhotoPrintService
    {
        private const float PointsPerInch = 72f;
        private const float PageWidth = 612f;
        private const float PageHeight = 792f;
        private const float PageMargin = 36f;

        public bool IsSupported => TryRun("lpstat", ["-r"], out var result) && result.ExitCode == 0;

        public IReadOnlyList<string> GetPrinters()
        {
            if (!TryRun("lpstat", ["-a"], out var result) || result.ExitCode != 0)
                return [];

            var printers = new List<string>();
            foreach (var line in result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var separator = line.IndexOfAny([' ', '\t']);
                var name = (separator < 0 ? line : line[..separator]).Trim();
                if (!string.IsNullOrEmpty(name))
                    printers.Add(name);
            }

            return printers;
        }

        public string DefaultPrinter
        {
            get
            {
                if (!TryRun("lpstat", ["-d"], out var result) || result.ExitCode != 0)
                    return null;

                var separator = result.StandardOutput.IndexOf(':');
                return separator < 0 ? null : result.StandardOutput[(separator + 1)..].Trim();
            }
        }

        public void Print(string printerName, PhotoPrintJob job, Action<bool, string> onComplete)
        {
            Task.Run(() =>
            {
                string path = null;
                try
                {
                    if (string.IsNullOrWhiteSpace(printerName))
                    {
                        onComplete?.Invoke(false, "No default printer is configured.");
                        return;
                    }

                    path = Path.Combine(Path.GetTempPath(), $"ladxhd-photo-{Guid.NewGuid():N}.pdf");
                    File.WriteAllBytes(path, CreatePdf(job));

                    var arguments = new List<string> { "-d", printerName, "-n", Math.Clamp(job.Copies, 1, 99).ToString(CultureInfo.InvariantCulture) };
                    if (!string.IsNullOrWhiteSpace(job.JobName))
                    {
                        arguments.Add("-t");
                        arguments.Add(job.JobName);
                    }
                    arguments.Add(path);

                    if (!TryRun("lp", arguments, out var result))
                    {
                        onComplete?.Invoke(false, "Could not start the CUPS print command.");
                        return;
                    }

                    if (result.ExitCode != 0)
                    {
                        var error = string.IsNullOrWhiteSpace(result.StandardError) ? result.StandardOutput : result.StandardError;
                        onComplete?.Invoke(false, string.IsNullOrWhiteSpace(error) ? "CUPS rejected the print job." : error.Trim());
                        return;
                    }

                    Debug.WriteLine($"[Print] Sent '{job.JobName}' to '{printerName}'.");
                    onComplete?.Invoke(true, null);
                }
                catch (Exception exception)
                {
                    Debug.WriteLine($"[Print] Failed: {exception}");
                    onComplete?.Invoke(false, exception.Message);
                }
                finally
                {
                    if (path != null)
                    {
                        try { File.Delete(path); }
                        catch (IOException) { }
                    }
                }
            });
        }

        private static bool TryRun(string command, IEnumerable<string> arguments, out CommandResult result)
        {
            result = default;
            try
            {
                var startInfo = new ProcessStartInfo(command)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                foreach (var argument in arguments)
                    startInfo.ArgumentList.Add(argument);

                using var process = Process.Start(startInfo);
                if (process == null)
                    return false;

                var standardOutput = process.StandardOutput.ReadToEnd();
                var standardError = process.StandardError.ReadToEnd();
                process.WaitForExit();
                result = new CommandResult(process.ExitCode, standardOutput, standardError);
                return true;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                return false;
            }
        }

        private static byte[] CreatePdf(PhotoPrintJob job)
        {
            if (job.Width <= 0 || job.Height <= 0 || job.Rgba == null || job.Rgba.Length < checked(job.Width * job.Height * 4))
                throw new ArgumentException("The photo data is invalid.", nameof(job));

            var imageData = new byte[checked(job.Width * job.Height * 3)];
            for (int source = 0, destination = 0; destination < imageData.Length; source += 4)
            {
                var r = job.Rgba[source];
                var g = job.Rgba[source + 1];
                var b = job.Rgba[source + 2];
                if (job.Grayscale)
                    r = g = b = (byte)((r * 77 + g * 151 + b * 28) >> 8);

                imageData[destination++] = r;
                imageData[destination++] = g;
                imageData[destination++] = b;
            }

            byte[] compressedImage;
            using (var stream = new MemoryStream())
            {
                using (var compressor = new ZLibStream(stream, CompressionLevel.Optimal, leaveOpen: true))
                    compressor.Write(imageData, 0, imageData.Length);
                compressedImage = stream.ToArray();
            }

            var width = Math.Min(Math.Max(job.WidthInches, 0.1f) * PointsPerInch, PageWidth - PageMargin * 2);
            var height = width * job.Height / job.Width;
            if (height > PageHeight - PageMargin * 2)
            {
                height = PageHeight - PageMargin * 2;
                width = height * job.Width / job.Height;
            }

            var x = (PageWidth - width) / 2f;
            var y = (PageHeight - height) / 2f;
            var contents = $"q\n{Format(width)} 0 0 {Format(height)} {Format(x)} {Format(y)} cm\n/Photo Do\nQ\n";

            using var pdf = new MemoryStream();
            var offsets = new List<long>();
            WriteAscii(pdf, "%PDF-1.4\n");
            WriteBytes(pdf, [(byte)'%', 0x80, 0x81, 0x82, 0x83, (byte)'\n']);
            WriteObject(pdf, offsets, 1, "<< /Type /Catalog /Pages 2 0 R >>");
            WriteObject(pdf, offsets, 2, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
            WriteObject(pdf, offsets, 3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /XObject << /Photo 4 0 R >> >> /Contents 5 0 R >>");
            WriteStreamObject(pdf, offsets, 4, $"<< /Type /XObject /Subtype /Image /Width {job.Width} /Height {job.Height} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /FlateDecode /Length {compressedImage.Length} >>", compressedImage);
            WriteStreamObject(pdf, offsets, 5, $"<< /Length {Encoding.ASCII.GetByteCount(contents)} >>", Encoding.ASCII.GetBytes(contents));

            var crossReferenceOffset = pdf.Position;
            WriteAscii(pdf, $"xref\n0 {offsets.Count + 1}\n0000000000 65535 f \n");
            foreach (var offset in offsets)
                WriteAscii(pdf, $"{offset:D10} 00000 n \n");
            WriteAscii(pdf, $"trailer\n<< /Size {offsets.Count + 1} /Root 1 0 R >>\nstartxref\n{crossReferenceOffset}\n%%EOF\n");
            return pdf.ToArray();
        }

        private static void WriteObject(Stream stream, List<long> offsets, int number, string value)
        {
            offsets.Add(stream.Position);
            WriteAscii(stream, $"{number} 0 obj\n{value}\nendobj\n");
        }

        private static void WriteStreamObject(Stream stream, List<long> offsets, int number, string dictionary, byte[] data)
        {
            offsets.Add(stream.Position);
            WriteAscii(stream, $"{number} 0 obj\n{dictionary}\nstream\n");
            stream.Write(data, 0, data.Length);
            WriteAscii(stream, "\nendstream\nendobj\n");
        }

        private static void WriteAscii(Stream stream, string value)
        {
            var bytes = Encoding.ASCII.GetBytes(value);
            stream.Write(bytes, 0, bytes.Length);
        }

        private static void WriteBytes(Stream stream, byte[] value)
        {
            stream.Write(value, 0, value.Length);
        }

        private static string Format(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);

        private readonly struct CommandResult
        {
            public CommandResult(int exitCode, string standardOutput, string standardError)
            {
                ExitCode = exitCode;
                StandardOutput = standardOutput;
                StandardError = standardError;
            }

            public int ExitCode { get; }
            public string StandardOutput { get; }
            public string StandardError { get; }
        }
    }
}
