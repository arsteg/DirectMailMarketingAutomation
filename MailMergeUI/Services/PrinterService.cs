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
            Log.Debug("Retrieving available printers");
            return new List<string> { "HP LaserJet Pro", "Brother MFC", "PDF Printer", "Microsoft Print to PDF" };
        }

        public async Task<bool> PrintAsync(string printerName, string content)
        {
            try
            {
                Log.Information("Starting print job on {PrinterName}", printerName);
                await Task.Delay(500);
                if (printerName.Contains("PDF") || printerName.Contains("Microsoft"))
                {
                    Log.Information("Print job succeeded on {PrinterName}", printerName);
                    return true;
                }
                    
                var result = new Random().Next(0, 10) > 1; // 90% success
                if (result) Log.Information("Print job succeeded on {PrinterName}", printerName);
                else Log.Warning("Print job failed on {PrinterName}", printerName);
                
                return result;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception during print job on {PrinterName}", printerName);
                return false;
            }
        }

        public async Task SavePdfAsync(string directory, string filename)
        {
            try
            {
                Log.Information("Saving PDF to {Directory}/{Filename}", directory, filename);
                await Task.Delay(300);
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, filename), "PDF Content");
                Log.Information("PDF saved successfully");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error saving PDF to {Directory}/{Filename}", directory, filename);
                throw;
            }
        }
    }
}
