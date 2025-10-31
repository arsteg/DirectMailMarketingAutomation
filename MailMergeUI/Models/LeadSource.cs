using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MailMergeUI.Models
{
    public class LeadSource
    {
        public string ApiUrl { get; set; } = "";
        public string ApiKey { get; set; } = "";
        public string FiltersJson { get; set; } = "{}"; // e.g., {"min_price": 100000, "city": "Austin"}
        public TimeSpan RunAt { get; set; } = new TimeSpan(6, 0, 0); // 6:00 AM
    }
}
