using MailMerge.Data;
using MailMerge.Data.Models;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MailMergeUI.Services
{
    public class DashboardService
    {
        private readonly MailMergeDbContext _context;

        public DashboardService(MailMergeDbContext dbContext)
        {
            _context = dbContext;
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


        // ================================================================
        // 1. Pending Letters to Print Today – Filtered by Campaign
        // ================================================================
        public async Task<int> GetPendingLettersTodayAsync(int campaignId)
        {
            var today = DateTime.Today;
            var now = DateTime.Now.TimeOfDay;
            int getTotalResults = 0;
            // Filter by specific campaignId
            var campaign = await _context.Campaigns
                .AsNoTracking()
                .Where(c => c.Id == campaignId && c.LeadSource != null)
                .Select(c => new
                {
                    c.LastRunningTime,
                    c.LeadSource.Type,
                    c.LeadSource,
                    c.LeadSource.RunAt,
                    c.ScheduledDate,
                    c.Printer,
                    DaysOfWeek = c.LeadSource.DaysOfWeek ?? new List<string>(),
                    Stages = c.Stages.Select(s => new
                    {
                        s.Id,
                        s.DelayDays,
                        s.IsRun,
                        s.IsFetched
                    }).ToList()
                })
                .FirstOrDefaultAsync();
           
            if (campaign == null) return 0;

            bool shouldRunToday = false;

            if (campaign.Type == ScheduleType.Daily && now >= campaign.RunAt)
                shouldRunToday = true;

            else if (campaign.Type == ScheduleType.None)
            {
                if (campaign.DaysOfWeek.Contains(today.DayOfWeek.ToString(), StringComparer.OrdinalIgnoreCase) &&
                    now >= campaign.RunAt)
                    shouldRunToday = true;
            }
            if (!shouldRunToday) return 0;

            var baseDate = campaign.ScheduledDate.Date == default(DateTime)
                                 ? DateTime.MinValue.Date
                                 : campaign.ScheduledDate.Date;

            int pending = 0;
            int pendingTodayStageId = 0;
           
            foreach (var stage in campaign.Stages)
            {
                if (!stage.IsRun && today == baseDate.AddDays(stage.DelayDays))
                {
                    pendingTodayStageId = stage.Id;    
                }
            }
            if (pendingTodayStageId < 0)
            { 
                return 0; 
            }
            var pendingTodayStage = campaign.Stages
                .Where(s => s.Id == pendingTodayStageId).FirstOrDefault();
            bool IsFetched = pendingTodayStage.IsFetched;

            if (!IsFetched)
            {
                // call api here
                string RequestedFields = "RadarID,APN,PType,Address,City,State,ZipFive,Owner,OwnershipType,PrimaryName,PrimaryFirstName,OwnerAddress,OwnerCity,OwnerZipFive,OwnerState,inForeclosure,ForeclosureStage,ForeclosureDocType,ForeclosureRecDate,isSameMailing,Trustee,TrusteePhone,TrusteeSaleNum";
                string url = "https://api.propertyradar.com/v1/properties";
                string? bearerToken = System.Configuration.ConfigurationManager.AppSettings["API Key"];

                if (string.IsNullOrEmpty(bearerToken))
                {
                    Log.Warning("API Key missing in configuration.");
                }

                string rawQueryParams = $"?Purchase=1&Fields={RequestedFields}";
                var _httpClient = new HttpClient();
                //await context.Database.EnsureCreatedAsync();
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

                int start = 0;
                int batchSize = 500;
                getTotalResults = 0;
                bool moreData = true;

                do
                {
                    // Build URL with pagination
                    var pagedUrl = $"{url}{rawQueryParams}&Start={start}";
                    Log.Debug("Fetching records starting from {Start}", start);

                    // Serialize request body
                    var jsonContent = campaign.LeadSource.FiltersJson;
                    var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                    // Send POST
                    var response = await _httpClient.PostAsync(pagedUrl, content);
                    if (!response.IsSuccessStatusCode)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        Log.Error("API Call Failed at start={Start}. Status: {StatusCode}. Details: {Details}", start, response.StatusCode, errorContent);
                        break;
                    }

                    // Deserialize response
                    var responseStream = await response.Content.ReadAsStreamAsync();
                    var apiResponse = await JsonSerializer.DeserializeAsync<ApiResponse>(responseStream);

                    if (apiResponse?.Results == null || !apiResponse.Results.Any())
                    {
                        Log.Information("No results found for batch starting at {Start}", start);
                        break;
                    }

                    getTotalResults = apiResponse.TotalResultCount;

                    // Map results
                    var propertiesToSave = apiResponse.Results
                        .Select(dto => MapToPropertyRecord(dto))
                        .ToList();

                    propertiesToSave.ForEach(x => x.CampaignId = campaignId);

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
                        existing.CampaignId = campaignId;
                    }

                    // ✅ Add new records
                    if (newProperties.Any())
                    {
                        await _context.Properties.AddRangeAsync(newProperties);
                        int saved = await _context.SaveChangesAsync();
                        Log.Information("Saved {Count} new properties.", saved);
                    }
                    else
                    {
                        Log.Debug("No new records to insert for batch starting {Start}.", start);
                    }
                    await _context.SaveChangesAsync();

                    start += batchSize;
                    moreData = start < getTotalResults;

                } while (moreData);

                var campaignToUpdate = await _context.Campaigns
    .FirstOrDefaultAsync(c => c.Id == campaignId);

                if (campaignToUpdate != null)
                {
                    campaignToUpdate.LastRunningTime = DateTime.Now;
                    await _context.SaveChangesAsync();
                }
    
                var newTodayStage = campaignToUpdate.Stages
                    .Where(s => s.Id == pendingTodayStageId).FirstOrDefault();

                campaignToUpdate.LastRunningTime = DateTime.Now;
                newTodayStage.IsFetched = true;
                await _context.SaveChangesAsync();
                var records = await _context.Properties.Where(x => x.CampaignId == campaignId && x.IsBlackListed == false).ToListAsync();

                foreach (var item in records)
                {
                    AddRecordToPrintHistory(item.Id, campaignToUpdate, newTodayStage, campaign.Printer,"");

                }

                pending = await _context.PrintHistory
               .Where(p => p.CampaignId == campaignId && p.PrintedAt.Date == today && p.StageId == pendingTodayStageId)
               .CountAsync();
            }
            else
            {
                pending= await _context.PrintHistory
                .Where(p => p.CampaignId == campaignId && p.PrintedAt.Date == today && p.StageId == pendingTodayStageId)
                .CountAsync();
            }

                return pending;
        }

        // ================================================================
        // 2. Due Letters Today – Filtered by Campaign
        // ================================================================
        public async Task<int> GetDueLettersTodayAsync(int campaignId)
        {
            var today = DateTime.Today;

            var campaign = await _context.Campaigns
                .AsNoTracking()
                .Where(c => c.Id == campaignId)
                .Select(c => new
                {
                    c.LastRunningTime,
                    Stages = c.Stages.Select(s => new { s.DelayDays, s.IsRun }).ToList()
                })
                .FirstOrDefaultAsync();

            if (campaign == null) return 0;

            var baseDate = campaign.LastRunningTime == default(DateTime)
                ? DateTime.MinValue.Date
                : campaign.LastRunningTime.Date;

            int due = 0;
            foreach (var s in campaign.Stages)
            {
                if (!s.IsRun && baseDate.AddDays(s.DelayDays) == today)
                    due++;
            }

            return due;
        }

        // ================================================================
        // 3. Letters Printed Today – Filtered by Campaign
        // ================================================================
        public async Task<int> GetLettersPrintedTodayAsync(int campaignId)
        {
            var today = DateTime.Today;
            return await _context.PrintHistory
                .Where(p => p.CampaignId == campaignId && p.PrintedAt.Date == today)
                .CountAsync();
        }

        // ================================================================
        // 4. Letters Printed This Month – Filtered by Campaign
        // ================================================================
        public async Task<int> GetLettersPrintedThisMonthAsync(int campaignId)
        {
            var startOfMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            return await _context.PrintHistory
                .Where(p => p.CampaignId == campaignId && p.PrintedAt >= startOfMonth)
                .CountAsync();
        }

        // ================================================================
        // 5. Pending Letters Today from API – Filtered by Campaign
        // ================================================================
        public async Task<int> GetPendingLettersTodayFromApiAsync(int campaignId, int apiPropertyCount)
        {
            var today = DateTime.Today;
            var now = DateTime.Now;

            // Filter by specific campaignId
            var campaign = await _context.Campaigns
                .AsNoTracking()
                .Where(c => c.Id == campaignId && c.LeadSource != null)
                .Select(c => new
                {
                    c.LastRunningTime,
                    c.LeadSource.Type,
                    c.LeadSource.RunAt,
                    c.ScheduledDate,
                    DaysOfWeek = c.LeadSource.DaysOfWeek ?? new List<string>(),
                    Stages = c.Stages.Select(s => new
                    {
                        s.DelayDays,
                        s.IsRun
                    }).ToList()
                })
                .FirstOrDefaultAsync();

            if (campaign == null) return 0;

            // ✅ Check if campaign is scheduled to run today
            bool isScheduledToday = false;

            if (campaign.Type == ScheduleType.Daily)
                isScheduledToday = true;
            else if (campaign.Type == ScheduleType.None) // Weekly schedule
            {
                if (campaign.DaysOfWeek.Contains(today.DayOfWeek.ToString(), StringComparer.OrdinalIgnoreCase))
                    isScheduledToday = true;
            }

            if (!isScheduledToday) return 0;

            // ✅ Check if RunAt time has passed
            bool runAtTimePassed = now.TimeOfDay >= campaign.RunAt;

            // ✅ If RunAt time has already passed today, no letters are pending (they should have been sent)
            if (runAtTimePassed)
            {
                // Check if campaign actually ran today
                if (campaign.LastRunningTime.Date == today)
                {
                    // Campaign ran today, so no pending letters
                    return 0;
                }
                // If campaign didn't run today despite RunAt time passing, 
                // it means there's an issue, but we still return 0 for "pending today"
                // as the scheduled time has passed
                return 0;
            }
            var baseDate = campaign.ScheduledDate.Date == default(DateTime)
                              ? DateTime.MinValue.Date
                              : campaign.ScheduledDate.Date;


            // ✅ Count stages that are due today or before (and haven't run yet)
            int pendingStages = 0;

            foreach (var stage in campaign.Stages)
            {
                if (!stage.IsRun)
                {
                    var stageDueDate = baseDate.AddDays(stage.DelayDays);

                    // Stage is pending if it's due today or before
                    if (stageDueDate <= today)
                        pendingStages++;
                }
            }

            return pendingStages * apiPropertyCount;
        }
        // ================================================================
        // 6. Due Tomorrow Letters from API – ONLY NEW stages
        // ================================================================
        public async Task<int> GetDueTomorrowFromApiAsync(int campaignId, int apiPropertyCount)
        {
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            var campaign = await _context.Campaigns
                .AsNoTracking()
                .Where(c => c.Id == campaignId && c.LeadSource != null)
                .Select(c => new
                {
                    c.LastRunningTime,
                    c.LeadSource.Type,
                    c.LeadSource.RunAt,
                    c.ScheduledDate,
                    DaysOfWeek = c.LeadSource.DaysOfWeek ?? new List<string>(),
                    Stages = c.Stages.Select(s => new
                    {
                        s.DelayDays,
                        s.IsRun
                    }).ToList()
                })
                .FirstOrDefaultAsync();

            if (campaign == null)
                return 0;

            // ✅ Check if the campaign is scheduled for tomorrow
            bool shouldRunTomorrow = false;

            if (campaign.Type == ScheduleType.Daily)
            {
                shouldRunTomorrow = true;
            }
            else if (campaign.Type == ScheduleType.None) // Weekly
            {
                if (campaign.DaysOfWeek.Contains(
                        tomorrow.DayOfWeek.ToString(),
                        StringComparer.OrdinalIgnoreCase))
                {
                    shouldRunTomorrow = true;
                }
            }

            if (!shouldRunTomorrow)
                return 0;

            // Base date for calculating stage due dates
            var baseDate = campaign.ScheduledDate.Date == default(DateTime)
                            ? DateTime.MinValue.Date
                            : campaign.ScheduledDate.Date;

            // -----------------------------------------
            // ✅ Count ONLY stages that are due tomorrow
            // -----------------------------------------
            int stageCount = 0;

            foreach (var stage in campaign.Stages)
            {
                if (!stage.IsRun)
                {
                    var stageDueDate = baseDate.AddDays(stage.DelayDays);

                    if (stageDueDate == tomorrow)      // ✔ EXACTLY tomorrow
                    {
                        stageCount++;
                    }
                }
            }

            // If no stage is due tomorrow → return 0
            if (stageCount == 0)
                return 0;

            return stageCount * apiPropertyCount;
        }

        // ================================================================
        // 7. Letters Printed Due Tomorrow – Filtered by Campaign (DB)
        // ================================================================
        public async Task<int> GetLettersPrintedDeuTomorrowAsync(int campaignId)
        {
            var today = DateTime.Today;

            var campaign = await _context.Campaigns
                                .AsNoTracking()
                                .Where(c => c.Id == campaignId && c.LeadSource != null)
                                .Select(c => new
                                {
                                    c.LastRunningTime,
                                    c.LeadSource.Type,
                                    c.LeadSource.RunAt,
                                    c.ScheduledDate,
                                    DaysOfWeek = c.LeadSource.DaysOfWeek ?? new List<string>(),
                                    Stages = c.Stages.Select(s => new
                                    {
                                        s.DelayDays,
                                        s.IsRun,
                                        s.Id                                
                                    }).ToList()
                                })
                                .FirstOrDefaultAsync();

            if (campaign == null)
                return 0;

            int stageIdToReturn = 0;

            // Order stages properly
            var orderedStages = campaign.Stages
                .OrderBy(s => s.Id) 
                .ToList();

            // Case 1: Only one stage → stop execution
            if (orderedStages.Count == 1)
            {
                return 0;
            }

            // Find last executed stage
            var lastRunStage = orderedStages
                .LastOrDefault(s => s.IsRun);

            // Case 2: All stages have run → stop execution
            if (lastRunStage != null && lastRunStage == orderedStages.Last())
            {
                return 0;
            }

            // Case 3: Some stages have run AND further stages exist
            if (lastRunStage != null)
            {
                stageIdToReturn = lastRunStage.Id;
            }

            // Case 4: No stage has run yet 
            if (lastRunStage == null)
            {
           var firstStage= orderedStages.First();

                return await _context.PrintHistory
            .Where(p => p.CampaignId == campaignId && p.PrintedAt.Date == today && p.StageId == firstStage.Id)
            .CountAsync();
            }

            return await _context.PrintHistory
                .Where(p => p.CampaignId == campaignId && p.PrintedAt.Date == today && p.StageId== stageIdToReturn)
                .CountAsync();
        }


        private async Task AddRecordToPrintHistory(int propertyId, Campaign campaign, FollowUpStage stage, string selectedPrinter, string pdfPath)
        {
            _context.PrintHistory.Add(new PrintHistory
            {
                PropertyId = propertyId,
                CampaignId = campaign.Id,
                StageId = stage.Id,
                PrinterName = selectedPrinter,
                FilePath = pdfPath
            });
            await _context.SaveChangesAsync();
        }

    }
}