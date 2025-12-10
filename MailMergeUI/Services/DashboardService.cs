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
    }
}