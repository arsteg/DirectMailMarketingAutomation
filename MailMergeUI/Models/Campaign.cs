using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing.Printing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MailMergeUI.Models
{
    public class Campaign
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public LeadSource LeadSource { get; set; } = new();
        public ObservableCollection<FollowUpStage> Stages { get; set; } = new();
        public PrinterSettings LetterPrinter { get; set; } = new();
        public PrinterSettings EnvelopePrinter { get; set; } = new();
        public bool IsActive { get; set; }
    }
}
