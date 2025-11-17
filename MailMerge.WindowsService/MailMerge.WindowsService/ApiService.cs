using MailMerge.Data;
using MailMerge.Data.Models;
using MailMergeEngine;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Serilog;

public class ApiService
{
    private readonly MailMergeEngine.MailMergeEngine _engine;
    private readonly MailMergeDbContext _context;

    public ApiService(MailMergeEngine.MailMergeEngine engine,MailMergeDbContext dbContext)
    {
        _context = dbContext;
        _engine = engine;
    }

    /// <summary>
    /// Performs a POST API call using the specific PropertyRadar criteria structure.
    /// It appends the raw query string (including Fields/Pagination) to the base URL.
    /// </summary>
    /// <param name="url">The base API endpoint URL (e.g., "https://api.propertyradar.com/v1/properties").</param>
    /// <param name="bearerToken">The Bearer Token for authorization.</param>
    /// <param name="rawQueryParams">The full query string, including '?' and all parameters (e.g., "?Purchase=1&Fields=...&page=1&pageSize=500").</param>
    /// <returns>The number of records successfully saved to the database.</returns>
    public async Task PostAndSavePropertyRecordsAsync()
    {
        string RequestedFields = "RadarID,APN,PType,Address,City,State,ZipFive,Owner,OwnershipType,PrimaryName,PrimaryFirstName,OwnerAddress,OwnerCity,OwnerZipFive,OwnerState,inForeclosure,ForeclosureStage,ForeclosureDocType,ForeclosureRecDate,isSameMailing,Trustee,TrusteePhone,TrusteeSaleNum";
        string url = "https://api.propertyradar.com/v1/properties";
        string? bearerToken = System.Configuration.ConfigurationManager.AppSettings["API Key"];
        string rawQueryParams = $"?Purchase=1&Fields={RequestedFields}";
        var _httpClient = new HttpClient();
        //await context.Database.EnsureCreatedAsync();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        if (_context.Campaigns.Any())
        {
            var campaigns = _context.Campaigns.ToList();
            foreach (var campaign in campaigns)
            {
                if (campaign == null || campaign.LeadSource == null)
                    continue;

                var scheduleType = campaign.LeadSource.Type;
                var runAt = campaign.LeadSource.RunAt; // TimeSpan (e.g. 00:00:00)
                var daysOfWeek = campaign.LeadSource.DaysOfWeek; // List<string>

                if (scheduleType == ScheduleType.Daily)
                {
                    // Compare only the time part of current DateTime with the TimeSpan
                    var nowTime = DateTime.Now;

                    if (nowTime.TimeOfDay >= runAt)
                    {
                        // ✅ Run your scheduled code for daily schedule
                        await RunCampaign(_context,_httpClient,campaign,url,rawQueryParams,bearerToken);
                        foreach (var stage in campaign.Stages)
                        {
                            if (!stage.IsRun)
                            {
                                if (DateTime.Now >= campaign.LastRunningTime.AddDays(stage.DelayDays))
                                {
                                    var records = await _context.Properties.Where(x => x.CampaignId == campaign.Id).ToListAsync();
                                    var templatePath = await _context.Templates.Where(x => x.Id.ToString() == stage.TemplateId).Select(x=>x.Path).FirstOrDefaultAsync();
                                    var outputPath = Path.Combine(campaign.OutputPath, stage.StageName);
                                    if (!Directory.Exists(outputPath))
                                    {
                                        Directory.CreateDirectory(outputPath);
                                    }
                                    _engine.ExportBatch(templatePath, records, Path.Combine(outputPath, $"{campaign.Name}.pdf"));
                                }

                                stage.IsRun = true;
                            }
                        }
                        
                    }
                }
                else if (scheduleType == ScheduleType.None)
                {
                    // Example: daysOfWeek = ["Monday", "Wednesday", "Friday"]
                    var today = DateTime.Now.DayOfWeek.ToString(); // e.g. "Monday"

                    if (daysOfWeek != null && daysOfWeek.Contains(today, StringComparer.OrdinalIgnoreCase))
                    {
                        // ✅ Run your scheduled code for specific days
                        if (DateTime.Now.TimeOfDay >= runAt)
                        {
                            await RunCampaign(_context,_httpClient,campaign, url, rawQueryParams, bearerToken);
                            foreach (var stage in campaign.Stages)
                            {
                                if (!stage.IsRun)
                                {
                                    if (DateTime.Now >= campaign.LastRunningTime.AddDays(stage.DelayDays))
                                    {
                                        var records = await _context.Properties.Where(x => x.CampaignId == campaign.Id).ToListAsync();
                                        var templatePath = await _context.Templates.Where(x => x.Id.ToString() == stage.TemplateId).Select(x => x.Path).FirstOrDefaultAsync();
                                        var outputPath = Path.Combine(campaign.OutputPath, stage.StageName);
                                        if (!Directory.Exists(outputPath))
                                        {
                                            Directory.CreateDirectory(outputPath);
                                        }
                                        _engine.ExportBatch(templatePath, records, Path.Combine(outputPath, $"{campaign.Name}.pdf"));
                                    }

                                    stage.IsRun = true;
                                }
                            }
                        }
                    }
                }
            }
        }
        //Console.WriteLine($"\nCompleted fetching all records. Total saved: {totalRecordsSaved} / {totalResults}.");
        //return totalRecordsSaved;
    }

    private async Task RunCampaign(MailMergeDbContext _context,HttpClient _httpClient,Campaign campaign,string url,string rawQueryParams,string bearerToken)
    {
        int start = 0;
        int batchSize = 500;
        int totalResults = 0;
        bool moreData = true;

        do
        {
            // Build URL with pagination
            var pagedUrl = $"{url}{rawQueryParams}&Start={start}";
            Console.WriteLine($"\nFetching records starting from {start}...");

            // Serialize request body
            var jsonContent = campaign.LeadSource.FiltersJson;
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            // Send POST
            var response = await _httpClient.PostAsync(pagedUrl, content);
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"\n--- API Call Failed at start={start}. Status: {response.StatusCode} ---\nDetails: {errorContent}");
                break;
            }

            // Deserialize response
            var responseStream = await response.Content.ReadAsStreamAsync();
            var apiResponse = await JsonSerializer.DeserializeAsync<ApiResponse>(responseStream);

            if (apiResponse?.Results == null || !apiResponse.Results.Any())
            {
                Console.WriteLine("\nNo results found for this batch.");
                break;
            }

            totalResults = apiResponse.TotalResultCount;

            // Map results
            var propertiesToSave = apiResponse.Results
                .Select(dto => MapToPropertyRecord(dto))
                .ToList();

            var radarIds = propertiesToSave.Select(p => p.RadarId).ToList();

            // Fetch existing records with matching RadarIds
            var existingRecords = await _context.Properties
                .Where(p => radarIds.Contains(p.RadarId))
                .ToListAsync();

            // Determine new records (those not in DB)
            var existingRadarIds = existingRecords.Select(p => p.RadarId).ToList();
            var newProperties = propertiesToSave
                .Where(p => !existingRadarIds.Contains(p.RadarId))
                .ToList();

            // ✅ Update CampaignId for all existing records
            foreach (var existing in existingRecords)
            {
                existing.CampaignId = campaign.Id; // <-- use your new campaignId variable here
            }

            // ✅ Add new records
            if (newProperties.Any())
            {
                await _context.Properties.AddRangeAsync(newProperties);
                // ✅ Save all changes (updates + inserts)
                int saved = await _context.SaveChangesAsync();
            }
            else
            {
                Console.WriteLine($"No new records to insert for batch starting {start}.");
            }
            await _context.SaveChangesAsync();
            // Check if more results remain
            start += batchSize;
            moreData = start < totalResults;

        } while (moreData);

        campaign.LastRunningTime = DateTime.Now;
        await _context.SaveChangesAsync();

    }

    private PropertyRecord MapToPropertyRecord(PropertyResultDto dto)
    {
        // Mapping logic must be updated to include the new fields from the URL
        return new PropertyRecord
        {
            RadarId = dto.RadarID ?? string.Empty,
            Apn = dto.APN ?? string.Empty,
            Type = dto.PType ?? string.Empty,
            Address = dto.Address ?? string.Empty,
            City = dto.City ?? string.Empty,
            State = dto.State ?? string.Empty,
            Zip = dto.ZipFive ?? string.Empty,
            OwnerOcc = dto.IsSameMailing.HasValue && dto.IsSameMailing.Value == 1 ? "1" : "0",

            Owner = dto.Owner ?? string.Empty,
            OwnerType = dto.OwnershipType ?? string.Empty,
            PrimaryName = dto.PrimaryName ?? string.Empty,
            PrimaryFirst = dto.PrimaryFirstName ?? string.Empty,

            MailAddress = dto.OwnerAddress ?? string.Empty,
            MailCity = dto.OwnerCity ?? string.Empty,
            MailState = dto.OwnerState ?? string.Empty,
            MailZip = dto.OwnerZipFive ?? string.Empty,

            Foreclosure = dto.InForeclosure.HasValue && dto.InForeclosure.Value == 1 ? "1" : "0",

            // NEW FIELDS based on the URL provided
            FclStage = dto.ForeclosureStage ?? string.Empty,
            FclDocType = dto.ForeclosureDocType ?? string.Empty,
            FclRecDate = dto.ForeclosureRecDate ?? string.Empty,
            Trustee = dto.Trustee ?? string.Empty,
            TrusteePhone = dto.TrusteePhone ?? string.Empty,
            TsNumber = dto.TrusteeSaleNum ?? string.Empty
        };
    }
}