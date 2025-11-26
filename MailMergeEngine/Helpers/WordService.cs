using System;
using System.IO;
using System.Threading.Tasks;
using MailMerge.Data.Models;
using Syncfusion.DocIO;
using Syncfusion.DocIO.DLS;

namespace MailMergeEngine.Helpers
{
    

    public class WordService
    {
        /// <summary>
        /// Opens a Word template, replaces placeholders in the format "{FieldName}", 
        /// and returns the filled document as a byte array.
        /// </summary>
        public async Task<byte[]> FillTemplate(string templatePath, PropertyRecord record)
        {
            // 1. Read the file into a MemoryStream or FileStream
            // We read bytes asynchronously to keep the signature async, though DocIO operations are largely synchronous.
            byte[] fileBytes = await File.ReadAllBytesAsync(templatePath);

            using (var stream = new MemoryStream(fileBytes))
            using (var document = new WordDocument(stream, FormatType.Docx))
            {
                // 2. Define a helper action to handle the Find/Replace logic safely
                // This looks for "{Key}" and replaces it with "Value"
                void ReplaceField(string placeholderName, string value)
                {
                    // The format specified is "{Name}", so we wrap the key in brackets
                    string searchPattern = $"{{{placeholderName}}}";

                    // Replace parameters:
                    // 1. matchString: The text to find
                    // 2. replaceString: The text to insert (handle nulls with empty string)
                    // 3. matchCase: True to match exact casing
                    // 4. matchWholeWord: True to ensure we don't replace inside other words
                    document.Replace(searchPattern, value ?? string.Empty, true, true);
                }

                // 3. Perform Replacements
                // Note: These keys match the text you write inside the Word Doc, e.g. {Radar ID}
                ReplaceField("Radar ID", record.RadarId);
                ReplaceField("Apn", record.Apn);
                ReplaceField("Type", record.Type);
                ReplaceField("Address", record.Address);
                ReplaceField("City", record.City);
                ReplaceField("State", record.State);
                ReplaceField("ZIP", record.Zip);
                ReplaceField("Owner", record.Owner);
                ReplaceField("Owner Type", record.OwnerType);
                ReplaceField("Owner Occ?", record.OwnerOcc);
                ReplaceField("Primary Name", record.PrimaryName);
                ReplaceField("Primary First", record.PrimaryFirst);
                ReplaceField("Mail Address", record.MailAddress);
                ReplaceField("Mail City", record.MailCity);
                ReplaceField("Mail State", record.MailState);
                ReplaceField("Mail ZIP", record.MailZip);
                ReplaceField("Foreclosure", record.Foreclosure);
                ReplaceField("FCL Stage", record.FclStage);
                ReplaceField("FCL Doc Type", record.FclDocType);
                ReplaceField("FCL Rec Date", record.FclRecDate);
                ReplaceField("Trustee", record.Trustee);
                ReplaceField("Trustee Phone", record.TrusteePhone);
                ReplaceField("TS Number", record.TsNumber);

                // 4. Save to MemoryStream
                using (var outStream = new MemoryStream())
                {
                    document.Save(outStream, FormatType.Docx);
                    return outStream.ToArray();
                }
            }
        }
    }
    
}
