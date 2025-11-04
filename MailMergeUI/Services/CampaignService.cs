using MailMerge.Data;
using MailMerge.Data.Models;
using MailMergeUI.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MailMergeUI.Services
{
    // Services/CampaignService.cs
    public class CampaignService
    {      
        private readonly JsonSerializerOptions _options = new() { WriteIndented = true };

        private readonly MailMergeDbContext _db;

        public List<Campaign> Campaigns { get; private set; } = new();
        public List<LetterTemplate> Templates { get; private set; } = new();

        public CampaignService(MailMergeDbContext db)
        {
            _db = db;
            LoadAll();
        }

        public void SaveCampaign(Campaign campaign) => Save(campaign);
        public void SaveTemplates() => SaveTemplate("templates.json", Templates);

        public void DeleteCampaign(Campaign c)
        {
            var campaign = _db.Campaigns.Where(x => x.Id == c.Id).FirstOrDefault();
            if(campaign != null)
            {
                _db.Campaigns.Remove(campaign);
                _db.SaveChanges();
            }           
        }

        private void LoadAll()
        {
            Campaigns = LoadCampaign() ?? new();
            Templates = Load<List<LetterTemplate>>("templates.json") ?? new();
        }

        private T? Load<T>(string path)
        {
            if (!File.Exists(path)) return default;
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<T>(json, _options);
        }

        private List<Campaign> LoadCampaign()
        {
            return _db.Campaigns.ToList();
        }

        private void Save(Campaign data)
        {
            try
            {
                var existing = _db.Campaigns.Where(x => x.Id ==data.Id).FirstOrDefault();
                if (existing != null)
                {
                    existing.Name = data.Name;
                    existing.LeadSource = data.LeadSource;
                    existing.Stages = data.Stages;
                    existing.LetterPrinter = data.LetterPrinter;
                    existing.EnvelopePrinter = data.EnvelopePrinter;
                    existing.IsActive = data.IsActive;
                }
                else
                {
                    _db.Campaigns.Add(data);
                }
                _db.SaveChanges();

            }
            catch (Exception)
            {
                //do nothing
            }

        }

        private void SaveTemplate<T>(string path, T data)
        {
            var json = JsonSerializer.Serialize(data, _options);
            File.WriteAllText(path, json);
        }
    }
}
