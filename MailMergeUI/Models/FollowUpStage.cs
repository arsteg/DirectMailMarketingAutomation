using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MailMergeUI.Models
{
    // Models/FollowUpStage.cs
    public class FollowUpStage
    {
        public string StageName { get; set; } = "Stage";
        public string TemplateId { get; set; } = ""; // References Template.Id
        public int DelayDays { get; set; } = 0;
    }
}
