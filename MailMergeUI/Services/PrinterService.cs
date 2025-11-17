using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Serilog;

namespace MailMergeUI.Services
{
    public class PrinterService
    {
        public List<string> GetAvailablePrinters()
        {
            return new List<string> { "HP LaserJet Pro", "Brother MFC", "PDF Printer", "Microsoft Print to PDF" };
        }

        public async Task<bool> PrintAsync(string printerName, string content)
        {
            await Task.Delay(500);
            if (printerName.Contains("PDF") || printerName.Contains("Microsoft"))
                return true;
            return new Random().Next(0, 10) > 1; // 90% success
        }

        public async Task SavePdfAsync(string directory, string filename)
        {
            await Task.Delay(300);
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, filename), "PDF Content");
        }
    }
}
