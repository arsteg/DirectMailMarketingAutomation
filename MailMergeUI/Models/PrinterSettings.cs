using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MailMergeUI.Models
{
    public class PrinterSettings
    {
        public bool IsAutomatic { get; set; } = false;
        public string SelectedPrinter { get; set; } = "";
        public string OutputDirectory { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
    }
}
