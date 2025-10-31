﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MailMerge.Data.Models
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

    public class FollowUpStage
    {
        public int Id { get; set; }
        public string StageName { get; set; } = "Stage";
        public string TemplateId { get; set; } = ""; // References Template.Id
        public int DelayDays { get; set; } = 0;
    }

    public class LeadSource
    {
        public int Id { get; set; }
        public string ApiUrl { get; set; } = "";
        public string ApiKey { get; set; } = "";
        public string FiltersJson { get; set; } = "{}"; // e.g., {"min_price": 100000, "city": "Austin"}
        public TimeSpan RunAt { get; set; } = new TimeSpan(6, 0, 0); // 6:00 AM
    }

    public class PrinterSettings
    {
        public int Id { get; set; }
        public bool IsAutomatic { get; set; } = false;
        public string SelectedPrinter { get; set; } = "";
        public string OutputDirectory { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
    }
}
