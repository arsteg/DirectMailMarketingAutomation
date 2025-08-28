using CsvHelper;
using System.Drawing;
using System.Drawing.Imaging;
using System.Formats.Asn1;
using System.Globalization;
using static System.Net.Mime.MediaTypeNames;
using Image = System.Drawing.Image;

namespace RisoFtMailMergeEngine
{
    public class RisoFtMailMergeEngine
    {
        // RISO FT default: A4 at 600 DPI => 4960 x 7016 px
        private const int Dpi = 600;
        private const int Width = 4960;  // 210mm * 600dpi / 25.4
        private const int Height = 7016; // 297mm * 600dpi / 25.4

        public List<Dictionary<string, string>> ReadCsv(string csvPath)
        {
            using var reader = new StreamReader(csvPath);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            var records = new List<Dictionary<string, string>>();
            csv.Read();
            csv.ReadHeader();
            var header = csv.Context.Reader.HeaderRecord;
            while (csv.Read())
            {
                var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var h in header)
                {
                    dict[h] = csv.GetField(h) ?? string.Empty;
                }
                records.Add(dict);
            }
            return records;
        }

        public void MergeToPng(string templatePath, Dictionary<string, string> record, string outPath)
        {
            using var bmp = new Bitmap(Width, Height);
            bmp.SetResolution(Dpi, Dpi);
            using var g = Graphics.FromImage(bmp);
            g.Clear(Color.White);
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;

            // Draw template background if provided
            if (!string.IsNullOrEmpty(templatePath) && File.Exists(templatePath))
            {
                using var tpl = Image.FromFile(templatePath);
                g.DrawImage(tpl, new Rectangle(0, 0, Width, Height));
            }

            // Default font
            using var font = new System.Drawing.Font("Arial", 48, FontStyle.Regular, GraphicsUnit.Pixel);
            var sf = new StringFormat(StringFormat.GenericTypographic)
            {
                Alignment = StringAlignment.Near,
                LineAlignment = StringAlignment.Near,
                FormatFlags = StringFormatFlags.LineLimit
            };

            // Build address block
            string addressBlock = $"{Get(record, "FirstName")} {Get(record, "LastName")}\n" +
                                  $"{Get(record, "Address1")}\n" +
                                  $"{Get(record, "Address2")}\n" +
                                  $"{Get(record, "City")}, {Get(record, "State")} {Get(record, "Zip")}";

            // Position roughly centered on A4
            var addrRect = new RectangleF(500, 1500, Width - 1000, 1200);
            g.DrawString(addressBlock, font, Brushes.Black, addrRect, sf);

            // Barcode area below address block
            string barcode = Get(record, "BarcodeData");
            if (!string.IsNullOrEmpty(barcode))
            {
                using var barcodeFont = new System.Drawing.Font("Arial", 72, FontStyle.Regular, GraphicsUnit.Pixel);
                var barcodeRect = new RectangleF(500, addrRect.Bottom + 300, Width - 1000, 200);
                g.DrawString(barcode, barcodeFont, Brushes.Black, barcodeRect, sf);
            }

            // Save result
            Directory.CreateDirectory(Path.GetDirectoryName(outPath) ?? ".");
            bmp.Save(outPath, ImageFormat.Png);
        }

        private static string Get(Dictionary<string, string> d, string key) =>
            d.TryGetValue(key, out var v) ? v : string.Empty;
    }

}
