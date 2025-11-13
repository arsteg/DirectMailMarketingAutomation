using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MailMergeUI.Models
{
    public class BlacklistedProperty
    {
        public string Address { get; set; } = "";
        public DateTime BlacklistedAt { get; set; } = DateTime.Now;
        public string? BlackListingReason { get; set; }
    }
}
