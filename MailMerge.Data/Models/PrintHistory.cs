using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MailMerge.Data.Models
{
    public class PrintHistory
    {
        [Key]
        public int Id { get; set; }
        public int PropertyId { get; set; }
        public int CampaignId { get; set; }
        public int StageId { get; set; }
        public string PrinterName { get; set; } = string.Empty;
        public DateTime PrintedAt { get; set; } = DateTime.Now;
        public string FilePath { get; set; } = string.Empty;
    }
}
