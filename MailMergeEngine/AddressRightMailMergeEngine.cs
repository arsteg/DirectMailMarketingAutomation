using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.Drawing.Imaging;
using CsvHelper;
using System.Globalization;
using PdfSharpCore.Pdf;
using PdfSharpCore.Drawing;
using MailMerge.Data;

namespace MailMergeEngine
{
    public class AddressRightMailMergeEngine
    {
        // Target size: 3" x 15" at 600 DPI => 1800 x 9000
        private const int Dpi = 600;
        private const int Width = 3 * Dpi;
        private const int Height = 15 * Dpi;

        public List<Lead> ReadCsv(string csvPath)
        {
            using var reader = new StreamReader(csvPath);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

            csv.Read();
            csv.ReadHeader();
            var header = csv.Context.Reader.HeaderRecord;

            var leads = new List<Lead>();

            while (csv.Read())
            {
                var lead = new Lead
                {
                    FirstName = csv.GetField("FirstName") ?? string.Empty,
                    LastName = csv.GetField("LastName") ?? string.Empty,
                    Address1 = csv.GetField("Address1") ?? string.Empty,
                    Address2 = csv.GetField("Address2") ?? string.Empty,
                    City = csv.GetField("City") ?? string.Empty,
                    State = csv.GetField("State") ?? string.Empty,
                    Zip = csv.GetField("Zip") ?? string.Empty,
                    BarcodeData = csv.GetField("BarcodeData") ?? string.Empty
                };

                leads.Add(lead);
            }

            using var db = new MailMergeDbContext();
            db.Database.EnsureCreated();  // Creates DB/tables if not exist
            foreach (var lead in leads)
            {
                bool exists = db.Leads.Any(l =>
                    l.Address1 == lead.Address1 &&
                    l.FirstName == lead.FirstName);

                if (!exists)
                {
                    db.Leads.Add(lead);
                }
            }
            db.SaveChanges();
            return leads;
        }

        public void MergeToPng(string templatePath, Lead record, string outPath)
        {
            // Constants for the AddressRight clean template (3" x 15" at 600 DPI)
            const int Dpi = 600;
            const int Width = 3 * Dpi;    // 1800
            const int Height = 15 * Dpi;  // 9000

            // Address block and barcode coordinates from the clean template
            var addrX = 190;
            var addrY = 800;
            var addrWidth = 1500;
            var addrHeight = 1200;
            var addressRect = new RectangleF(addrX + 20, addrY + 20, addrWidth - 40, addrHeight - 40); // inner safe area

            var barcodeX = addrX;
            var barcodeY = 2400;
            var barcodeWidth = 1200;
            var barcodeHeight = 300;
            var barcodeRect = new RectangleF(barcodeX + 20, barcodeY + 20, barcodeWidth - 40, barcodeHeight - 40);

            // Create bitmap and graphics
            using var bmp = new Bitmap(Width, Height);
            bmp.SetResolution(Dpi, Dpi);
            using var g = Graphics.FromImage(bmp);
            g.Clear(Color.White);
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;

            // If template exists, draw it scaled to full canvas
            if (!string.IsNullOrEmpty(templatePath) && File.Exists(templatePath))
            {
                using var tpl = Image.FromFile(templatePath);
                // draw template filling entire canvas (assumes template is same aspect or background)
                g.DrawImage(tpl, new Rectangle(0, 0, Width, Height));
            }

            // Prepare fonts and formatting
            var fontFamily = new FontFamily("Arial"); // fallback; adjust if specific font required
                                                      // We'll start with a nominal font size and shrink if text won't fit
            float nameSizeStart = 110f;   // pixels (approx)
            float lineSizeStart = 100f;
            float minFontSize = 36f;

            var sfWrap = new StringFormat(StringFormat.GenericTypographic)
            {
                Alignment = StringAlignment.Near,
                LineAlignment = StringAlignment.Near,
                FormatFlags = StringFormatFlags.MeasureTrailingSpaces
            };

            // Build address lines
            string lineName = record.FirstName + " " + record.LastName;
            string lineAddress1 = record.Address1;
            string lineAddress2 = record.Address2;
            string cityLine = $"{record.City}, {record.State} {record.Zip}".Trim();

            // Create a single block text to layout (keeps line spacing consistent)
            var addressLines = new List<string>();
            if (!string.IsNullOrEmpty(lineName)) addressLines.Add(lineName);
            if (!string.IsNullOrEmpty(lineAddress1)) addressLines.Add(lineAddress1);
            if (!string.IsNullOrEmpty(lineAddress2)) addressLines.Add(lineAddress2);
            if (!string.IsNullOrEmpty(cityLine)) addressLines.Add(cityLine);

            // Join with newline for DrawString into RectangleF (handles wrapping & line breaks)
            string addressBlockText = string.Join("\n", addressLines);

            // Find a font size that fits into addressRect: reduce font size until measured height fits
            float testFontSize = lineSizeStart;
            Font testFont = null;
            RectangleF measureRect = addressRect;
            // We'll measure with Graphics.MeasureString (GenericTypographic is better for accurate measurement)
            while (testFontSize >= minFontSize)
            {
                testFont?.Dispose();
                testFont = new Font(fontFamily, testFontSize, FontStyle.Bold, GraphicsUnit.Pixel);
                var sizeF = g.MeasureString(addressBlockText, testFont, new SizeF(measureRect.Width, measureRect.Height), sfWrap);
                if (sizeF.Height <= measureRect.Height && sizeF.Width <= measureRect.Width * 1.05) // small width tolerance
                    break;
                testFontSize -= 4f; // step down
            }
            // Fallback if loop ended without a font (shouldn't happen unless extremely long lines)
            if (testFont == null)
            {
                testFont = new Font(fontFamily, minFontSize, FontStyle.Bold, GraphicsUnit.Pixel);
            }

            // Draw address block ? No — using clean template, so we don't overwrite background.
            // Draw the address block tebackgroundxt inside addressRect (top-left)
            using (var textBrush = new SolidBrush(Color.Black))
            {
                // Center vertically inside the address block if content height smaller than rect
                var measured = g.MeasureString(addressBlockText, testFont, new SizeF(measureRect.Width, measureRect.Height), sfWrap);
                float yOffset = 0;
                if (measured.Height < measureRect.Height)
                {
                    // Add a top offset so text appears vertically centered-ish (or keep it at top by leaving 0)
                    yOffset = Math.Max(0, (measureRect.Height - measured.Height) / 6.0f); // slight top bias
                }

                var drawRect = new RectangleF(measureRect.X, measureRect.Y + yOffset, measureRect.Width, measureRect.Height - yOffset);
                // Use LineLimit so long words don't overflow beyond rectangle
                sfWrap.FormatFlags |= StringFormatFlags.LineLimit;
                g.DrawString(addressBlockText, testFont, textBrush, drawRect, sfWrap);
            }

            // Draw barcode text centered vertically in barcodeRect (single-line)
            string barcodeText = record.BarcodeData;
            if (!string.IsNullOrEmpty(barcodeText))
            {
                // Choose a medium font for barcode text (not an actual barcode font — replace if you have one)
                float barcodeFontSize = 80f;
                Font barcodeFont = new Font(fontFamily, barcodeFontSize, FontStyle.Regular, GraphicsUnit.Pixel);
                // shrink if too wide
                var measureBarcode = g.MeasureString(barcodeText, barcodeFont);
                while (measureBarcode.Width > barcodeRect.Width && barcodeFontSize > 20f)
                {
                    barcodeFontSize -= 2f;
                    barcodeFont.Dispose();
                    barcodeFont = new Font(fontFamily, barcodeFontSize, FontStyle.Regular, GraphicsUnit.Pixel);
                    measureBarcode = g.MeasureString(barcodeText, barcodeFont);
                }
                // center vertically in barcodeRect
                float barcodeDrawY = barcodeRect.Y + (barcodeRect.Height - measureBarcode.Height) / 2f;
                g.DrawString(barcodeText, barcodeFont, Brushes.Black, barcodeRect.X, barcodeDrawY, sfWrap);
                barcodeFont.Dispose();
            }

            // Optional: debug outlines (comment out in production)
            // using (var pen = new Pen(Color.FromArgb(120, Color.Red), 3)) g.DrawRectangle(pen, addrX, addrY, addrWidth, addrHeight);
            // using (var pen = new Pen(Color.FromArgb(120, Color.Blue), 3)) g.DrawRectangle(pen, barcodeX, barcodeY, barcodeWidth, barcodeHeight);

            // Ensure directory exists
            if (Directory.Exists(outPath))
                Directory.Delete(outPath);

            Directory.CreateDirectory(Path.GetDirectoryName(outPath) ?? ".");

            // Save PNG (System.Drawing keeps DPI metadata)
            bmp.Save(outPath, ImageFormat.Png);

            // Dispose temporary font
            testFont.Dispose();
        }

        // Helper used above (if not already present in your class)
        // private static string Get(Dictionary<string,string> d, string key) => d.TryGetValue(key, out var v) ? v : string.Empty;


        public void CombinePngsToPdf(string folder, string outPdfPath)
        {
            if (File.Exists(outPdfPath))
                File.Delete(outPdfPath);

            double pageWidth = XUnit.FromPoint(Width * 72.0 / Dpi);
            double pageHeight = XUnit.FromPoint(Height * 72.0 / Dpi);

            var pngs = Directory.GetFiles(folder, "merged_*.png")
                               .OrderBy(x => x)
                               .ToArray();

            using var doc = new PdfDocument();

            foreach (var p in pngs)
            {
                using var img = XImage.FromFile(p);
                var page = doc.AddPage();
                page.Width = pageWidth;
                page.Height = pageHeight;

                using var gfx = XGraphics.FromPdfPage(page);
                gfx.DrawImage(img, 0, 0, page.Width, page.Height);
            }

            // Use File.Create instead of File.OpenWrite for better performance
            using var stream = File.Create(outPdfPath);
            doc.Save(stream, false); // Set 'closeStream' to false since we're managing it
        }
    }
}