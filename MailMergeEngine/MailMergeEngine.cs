using CsvHelper;
using iText.Forms;
using iText.Kernel.Pdf;
using MailMerge.Data;
using MailMerge.Data.Models;
using PdfSharpCore.Drawing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;

namespace MailMergeEngine
{
    public class MailMergeEngine
    {
        // Target size: 3" x 15" at 600 DPI => 1800 x 9000
        private const int Dpi = 600;
        private const int Width = 3 * Dpi;
        private const int Height = 15 * Dpi;

        public List<PropertyRecord> ReadCsv(string csvPath)
        {
            using var reader = new StreamReader(csvPath);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

            csv.Read();
            csv.ReadHeader();
            var header = csv.Context.Reader.HeaderRecord;

            var leads = new List<PropertyRecord>();

            while (csv.Read())
            {
                var lead = new PropertyRecord
                {
                    RadarId = csv.GetField("Radar ID") ?? string.Empty,
                    Apn = csv.GetField("APN") ?? string.Empty,
                    Type = csv.GetField("Type") ?? string.Empty,
                    Address = csv.GetField("Address") ?? string.Empty,
                    City = csv.GetField("City") ?? string.Empty,
                    State = csv.GetField("State") ?? string.Empty,
                    Zip = csv.GetField("ZIP") ?? string.Empty,
                    Owner = csv.GetField("Owner") ?? string.Empty,
                    OwnerType = csv.GetField("Owner Type") ?? string.Empty,
                    OwnerOcc = csv.GetField("Owner Occ?") ?? string.Empty,
                    PrimaryName = csv.GetField("Primary Name") ?? string.Empty,
                    PrimaryFirst = csv.GetField("Primary First") ?? string.Empty,
                    MailAddress = csv.GetField("Mail Address") ?? string.Empty,
                    MailCity = csv.GetField("Mail City") ?? string.Empty,
                    MailState = csv.GetField("Mail State") ?? string.Empty,
                    MailZip = csv.GetField("Mail ZIP") ?? string.Empty,
                    Foreclosure = csv.GetField("Foreclosure?") ?? string.Empty,
                    FclStage = csv.GetField("FCL Stage") ?? string.Empty,
                    FclDocType = csv.GetField("FCL Doc Type") ?? string.Empty,
                    FclRecDate = csv.GetField("FCL Rec Date") ?? string.Empty,
                    Trustee = csv.GetField("Trustee") ?? string.Empty,
                    TrusteePhone = csv.GetField("Trustee Phone") ?? string.Empty,
                    TsNumber = csv.GetField("TS Number") ?? string.Empty
                };

                leads.Add(lead);
            }

            using var db = new MailMergeDbContext();
            db.Database.EnsureCreated();  // Creates DB/tables if not exist
            foreach (var lead in leads)
            {
                bool exists = db.Properties.Any(l =>
                    l.PrimaryName == lead.PrimaryName &&
                    l.Address == lead.Address);

                if (!exists)
                {
                    db.Properties.Add(lead);
                }
            }
            db.SaveChanges();
            return db.Properties.ToList();
        }

        public void CombinePngsToPdf(string folder, string outPdfPath)
        {
            if (File.Exists(outPdfPath))
                File.Delete(outPdfPath);

            var pngs = Directory.GetFiles(folder, "merged_*.png")
                                .OrderBy(x => x)
                                .ToArray();

            using var doc = new PdfSharpCore.Pdf.PdfDocument();

            foreach (var p in pngs)
            {
                using var img = XImage.FromFile(p);

                // Calculate PDF page size directly from image pixel size & DPI
                double pageWidth = XUnit.FromPoint(img.PixelWidth * 72.0 / img.HorizontalResolution);
                double pageHeight = XUnit.FromPoint(img.PixelHeight * 72.0 / img.VerticalResolution);

                var page = doc.AddPage();
                page.Width = pageWidth;
                page.Height = pageHeight;

                using var gfx = XGraphics.FromPdfPage(page);
                gfx.DrawImage(img, 0, 0, page.Width, page.Height);
            }

            using var stream = File.Create(outPdfPath);
            doc.Save(stream, false);
        }


        public void MergePdfToPng(string templatePath, PropertyRecord record, string outPath)
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
                    if (fields.ContainsKey(field))
                        fields[field].SetValue(value);
                }

                SetField("Radar ID", record.RadarId);
                SetField("APN", record.Apn);
                SetField("Type", record.Type);
                SetField("Address", record.Address);
                SetField("City", record.City);
                SetField("State", record.State);
                SetField("ZIP", record.Zip);
                SetField("Owner", record.Owner);
                SetField("Owner Type", record.OwnerType);
                SetField("Owner Occ?", record.OwnerOcc);
                SetField("Primary Name", record.PrimaryName);
                SetField("Primary First", record.PrimaryFirst);
                SetField("Mail Address", record.MailAddress);
                SetField("Mail City", record.MailCity);
                SetField("Mail State", record.MailState);
                SetField("Mail ZIP", record.MailZip);
                SetField("Foreclosure", record.Foreclosure);
                SetField("FCL Stage", record.FclStage);
                SetField("FCL Doc Type", record.FclDocType);
                SetField("FCL Rec Date", record.FclRecDate);
                SetField("Trustee", record.Trustee);
                SetField("Trustee Phone", record.TrusteePhone);
                SetField("TS Number", record.TsNumber);

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

    }
}