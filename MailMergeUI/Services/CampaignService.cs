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

        public List<Campaign> Campaigns { get; private set; } = new();
        public List<LetterTemplate> Templates { get; private set; } = new();

        public CampaignService()
        {
            LoadAll();
        }

        public void SaveCampaigns() => Save(FilePath, Campaigns);
        public void SaveTemplates() => Save("templates.json", Templates);

        private void LoadAll()
        {
            Campaigns = Load<List<Campaign>>(FilePath) ?? new();
            Templates = Load<List<LetterTemplate>>("templates.json") ?? new();
        }

        private T? Load<T>(string path)
        {
            if (!File.Exists(path)) return default;
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<T>(json, _options);
        }

        private void Save<T>(string path, T data)
        {
            var json = JsonSerializer.Serialize(data, _options);
            File.WriteAllText(path, json);
        }
    }
}
