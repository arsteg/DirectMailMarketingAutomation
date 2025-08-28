using CsvHelper;
using iText.Forms;
using iText.Forms.Fields;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using iText.Layout;
using MailMerge.Data;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Formats.Asn1;
using System.Globalization;
using System.Reflection;
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

        public static string PdfToPng(string pdfPath, int page = 1, int dpi = 300)
        {
            string outputPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".png");

            var psi = new ProcessStartInfo
            {
                FileName = "gswin64c.exe", // Ghostscript
                Arguments = $"-dNOPAUSE -dBATCH -dSAFER -sDEVICE=png16m -r{dpi} " +
                            $"-dFirstPage={page} -dLastPage={page} " +
                            $"-sOutputFile=\"{outputPath}\" \"{pdfPath}\"",
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
               
            };

            using var proc = Process.Start(psi);
            proc.WaitForExit();

            if (proc.ExitCode != 0)
            {
                string error = proc.StandardError.ReadToEnd();
                throw new Exception($"Ghostscript failed: {error}");
            }

            return outputPath;
        }


        public void MergeToPng(string templatePath, Lead record, string outPath)
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
                string rasterized = PdfToPng(templatePath, 1, 300); // convert first page
                using var tpl = Image.FromFile(rasterized);
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
            string addressBlock = $"{record.FirstName} {record.LastName}\n" +
                                  $"{record.Address1}\n" +
                                  $"{record.Address2}\n" +
                                  $"{record.City}, {record.State} {record.Zip}";

            // Position roughly centered on A4
            var addrRect = new RectangleF(500, 1500, Width - 1000, 1200);
            g.DrawString(addressBlock, font, Brushes.Black, addrRect, sf);

            // Barcode area below address block
            string barcode = record.BarcodeData;
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

        public void CombinePngsToPdf(string folder, string outPdfPath)
        {
            if (File.Exists(outPdfPath))
                File.Delete(outPdfPath);

            double pageWidth = XUnit.FromPoint(Width * 72.0 / Dpi);
            double pageHeight = XUnit.FromPoint(Height * 72.0 / Dpi);

            var pngs = Directory.GetFiles(folder, "merged_*.png")
                               .OrderBy(x => x)
                               .ToArray();

            using var doc = new PdfSharpCore.Pdf.PdfDocument();

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


        public void MergePdfToPng(string templatePath, Lead record, string outPath)
        {
            if (!File.Exists(templatePath))
                throw new FileNotFoundException("Template PDF not found", templatePath);

            string filledPdf = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".pdf");

            // Step 1: Fill placeholders (using AcroForm fields instead of text replace)
            using (var pdfReader = new PdfReader(templatePath))
            using (var pdfWriter = new PdfWriter(filledPdf))
            using (var pdfDoc = new iText.Kernel.Pdf.PdfDocument(pdfReader, pdfWriter))
            {
                var form = PdfAcroForm.GetAcroForm(pdfDoc, true);
                var fields = form.GetAllFormFields();

                void SetField(string field, string value)
                {
                    if (fields.ContainsKey(field) && !string.IsNullOrEmpty(value))
                        fields[field].SetValue(value);
                }

                SetField("FirstName", record.FirstName);
                SetField("LastName", record.LastName);
                SetField("Address1", record.Address1);
                SetField("Address2", record.Address2);
                SetField("City", record.City);
                SetField("State", record.State);
                SetField("Zip", record.Zip);
                SetField("BarcodeData", record.BarcodeData);

                form.FlattenFields(); // makes fields permanent
                pdfDoc.Close();
            }

            // Step 2: Convert filled PDF → PNG using Ghostscript
            Directory.CreateDirectory(Path.GetDirectoryName(outPath) ?? ".");

            var psi = new ProcessStartInfo
            {
                FileName = "gswin64c.exe", // Ghostscript CLI
                Arguments = $"-dNOPAUSE -dBATCH -sDEVICE=png16m -r300 -sOutputFile=\"{outPath}\" \"{filledPdf}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                string error = process.StandardError.ReadToEnd();
                throw new Exception($"Ghostscript conversion failed: {error}");
            }
        }


    }

}
