using MailMerge.Data;
using MailMerge.Data.Models;
using MailMergeUI.Models;
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
        private const string FilePath = "campaigns.json";
        
        private readonly JsonSerializerOptions _options = new() { WriteIndented = true };

        private readonly MailMergeDbContext _db;

        public List<Campaign> Campaigns { get; private set; } = new();
        public List<LetterTemplate> Templates { get; private set; } = new();

        public CampaignService(MailMergeDbContext db)
        {
            _db = db;
            LoadAll();
        }

        public void SaveCampaigns() => Save(Campaigns);
        public void SaveTemplates() => SaveTemplate("templates.json", Templates);

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

        private void Save(List<Campaign> data)
        {
            _db.Campaigns.AddRange(data);
            _db.SaveChanges();

        }

        private void SaveTemplate<T>(string path, T data)
        {
            var json = JsonSerializer.Serialize(data, _options);
            File.WriteAllText(path, json);
        }
    }
}
