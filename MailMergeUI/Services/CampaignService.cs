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
using Serilog;

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
            try
            {
                var campaign = _db.Campaigns.FirstOrDefault(x => x.Id == c.Id);
                if (campaign != null)
                {
                    _db.Campaigns.Remove(campaign);
                    _db.SaveChanges();
                    Log.Information("Deleted campaign: {CampaignName} ({Id})", c.Name, c.Id);
                }
                else
                {
                    Log.Warning("Attempted to delete non-existent campaign: {Id}", c.Id);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error deleting campaign: {Id}", c.Id);
            }
        }

        private void LoadAll()
        {
            try
            {
                Campaigns = LoadCampaign() ?? new();
                Templates = Load<List<LetterTemplate>>("templates.json") ?? new();
                Log.Debug("Loaded {CampaignCount} campaigns and {TemplateCount} templates", Campaigns.Count, Templates.Count);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error loading initial data in CampaignService");
                Campaigns = new();
                Templates = new();
            }
        }

        private T? Load<T>(string path)
        {
            try
            {
                if (!File.Exists(path)) 
                {
                    Log.Debug("File not found for loading: {Path}", path);
                    return default;
                }
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<T>(json, _options);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error loading JSON from {Path}", path);
                return default;
            }
        }

        private List<Campaign> LoadCampaign()
        {
            try
            {
                return _db.Campaigns.ToList();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error loading campaigns from DB");
                return null;
            }
        }

        private void Save(Campaign data)
        {
            try
            {
                var existing = _db.Campaigns.FirstOrDefault(x => x.Id == data.Id);
                if (existing != null)
                {
                    existing.Name = data.Name;
                    existing.LeadSource = data.LeadSource;
                    existing.Stages = data.Stages;
                    existing.LetterPrinter = data.LetterPrinter;
                    existing.EnvelopePrinter = data.EnvelopePrinter;
                    existing.IsActive = data.IsActive;
                    existing.Printer = data.Printer;
                    Log.Information("Updated campaign: {Name} ({Id})", data.Name, data.Id);
                }
                else
                {
                    _db.Campaigns.Add(data);
                    Log.Information("Added new campaign: {Name} ({Id})", data.Name, data.Id);
                }
                _db.SaveChanges();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error saving campaign: {Name}", data.Name);
            }
        }

        private void SaveTemplate<T>(string path, T data)
        {
            try
            {
                var json = JsonSerializer.Serialize(data, _options);
                File.WriteAllText(path, json);
                Log.Information("Saved template data to {Path}", path);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error saving template to {Path}", path);
            }
        }
    }
}
