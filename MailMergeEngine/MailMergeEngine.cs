using CsvHelper;
using iText.Forms;
using iText.Kernel.Pdf;
using MailMerge.Data;
using MailMerge.Data.Helpers;
using MailMerge.Data.Models;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MailMergeEngine
{
    public class MailMergeEngine
    {
        private readonly MailMergeDbContext _db;

        public MailMergeEngine(MailMergeDbContext db)
        {
            _db = db;
            _db.Database.EnsureCreated();
        }

        public List<PropertyRecord> ReadCsv(string csvPath)
        {
            using var reader = new StreamReader(csvPath);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

            csv.Read();
            csv.ReadHeader();

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

            // ✅ Load all existing keys in memory once
            var existingKeys = _db.Properties
                .Select(l => new { l.PrimaryName, l.Address })
                .ToHashSet();

            // ✅ Only add non-existing
            var newLeads = leads
                .Where(l => !existingKeys.Contains(new { l.PrimaryName, l.Address }))
                .ToList();

            if (newLeads.Any())
            {
                _db.Properties.AddRange(newLeads);  // Bulk add
                _db.SaveChanges();
            }

            return _db.Properties.ToList();
        }

        public async Task SaveTemplate(Template template)
        {
            if (template == null)
                return;

            _db.Templates.Add(template);
            _db.SaveChanges();
        }

        public byte[] FillTemplate(string templatePath, PropertyRecord record)
        {
            using (var templateReader = new PdfReader(templatePath))
            using (var ms = new MemoryStream())
            {
                using (var writer = new PdfWriter(ms))
                using (var pdfDoc = new iText.Kernel.Pdf.PdfDocument(templateReader, writer))
                {
                    var form = PdfAcroForm.GetAcroForm(pdfDoc, true);
                    var fields = form.GetAllFormFields();

                    void SetField(string field, string value)
                    {
                        if (fields.ContainsKey(field))
                            fields[field].SetValue(value ?? "");
                    }

                    SetField("Radar ID", record.RadarId);
                    SetField("Apn", record.Apn);
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

                    form.FlattenFields();
                }
                return ms.ToArray();
            }
        }

        public void ExportBatch(string templatePath, IEnumerable<PropertyRecord> records, string outputPath)
        {
            using (var writer = new PdfWriter(outputPath))
            using (var pdfDoc = new iText.Kernel.Pdf.PdfDocument(writer))
            {
                pdfDoc.InitializeOutlines();

                foreach (var r in records)
                {
                    byte[] filled = FillTemplate(templatePath, r);
                    using (var filledDoc = new iText.Kernel.Pdf.PdfDocument(new PdfReader(new MemoryStream(filled))))
                    {
                        filledDoc.CopyPagesTo(1, filledDoc.GetNumberOfPages(), pdfDoc);
                    }
                }
            }
        }

        public bool ValidateUser(string user,string password)
        {
            var hashedPassword = PasswordHelper.HashPassword(password);
            var record = _db.Users.Where(x => user == x.Email && hashedPassword == x.Password).FirstOrDefault();
            return record != null;
        }

    }
}