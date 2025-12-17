using MailMerge.Data;
using MailMerge.Data.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
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

        // ================================================================
        // 1. Pending Letters to Print Today – Filtered by Campaign
        // ================================================================
        public async Task<int> GetPendingLettersTodayAsync(int campaignId)
        {
            var today = DateTime.Today;
            var now = DateTime.Now.TimeOfDay;

            // Filter by specific campaignId
            var campaign = await _context.Campaigns
                .AsNoTracking()
                .Where(c => c.Id == campaignId && c.LeadSource != null)
                .Select(c => new
                {
                    c.LastRunningTime,
                    c.LeadSource.Type,
                    c.LeadSource.RunAt,
                    DaysOfWeek = c.LeadSource.DaysOfWeek ?? new List<string>(),
                    Stages = c.Stages.Select(s => new
                    {
                        s.DelayDays,
                        s.IsRun
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

            var baseDate = campaign.LastRunningTime == default(DateTime)
                ? DateTime.MinValue.Date
                : campaign.LastRunningTime.Date;

            int pending = 0;
            foreach (var stage in campaign.Stages)
            {
                if (!stage.IsRun && today >= baseDate.AddDays(stage.DelayDays))
                    pending++;          
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

            // Case 4: No stage has run yet → also stop
            if (lastRunStage == null)
            {
                return 0;
            }

            return await _context.PrintHistory
                .Where(p => p.CampaignId == campaignId && p.PrintedAt.Date == today && p.StageId== stageIdToReturn)
                .CountAsync();
        }


    }
}