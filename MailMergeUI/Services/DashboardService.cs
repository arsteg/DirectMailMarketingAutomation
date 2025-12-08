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
        // 1. Pending Letters to Print Today – FULLY WORKING (No EF.Property!)
        // ================================================================
        public async Task<int> GetPendingLettersTodayAsync()
        {
            var today = DateTime.Today;
            var now = DateTime.Now.TimeOfDay;

            // This query is 100% translatable – we only project what we need
            var campaigns = await _context.Campaigns
                .AsNoTracking()
                .Where(c => c.LeadSource != null) // Ensure LeadSource exists
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
                .ToListAsync(); // ← This is fine – very small dataset (your campaigns, not 100k records)

            int pending = 0;

            foreach (var c in campaigns)
            {
                bool shouldRunToday = false;

                if (c.Type == ScheduleType.Daily && now >= c.RunAt)
                    shouldRunToday = true;

                else if (c.Type == ScheduleType.None)
                {
                    if (c.DaysOfWeek.Contains(today.DayOfWeek.ToString(), StringComparer.OrdinalIgnoreCase) &&
                        now >= c.RunAt)
                        shouldRunToday = true;
                }

                if (!shouldRunToday) continue;

                var baseDate = c.LastRunningTime == default(DateTime)
                    ? DateTime.MinValue.Date
                    : c.LastRunningTime.Date;

                foreach (var stage in c.Stages)
                {
                    if (!stage.IsRun && today >= baseDate.AddDays(stage.DelayDays))
                        pending++;
                }
            }

            return pending;
        }

        // ================================================================
        // 2. Due Letters Today
        // ================================================================
        public async Task<int> GetDueLettersTodayAsync()
        {
            var today = DateTime.Today;

            var campaigns = await _context.Campaigns
                .AsNoTracking()
                .Select(c => new
                {
                    c.LastRunningTime,
                    Stages = c.Stages.Select(s => new { s.DelayDays, s.IsRun }).ToList()
                })
                .ToListAsync();

            int due = 0;
            foreach (var c in campaigns)
            {
                var baseDate = c.LastRunningTime == default(DateTime)
                    ? DateTime.MinValue.Date
                    : c.LastRunningTime.Date;

                foreach (var s in c.Stages)
                {
                    if (!s.IsRun && baseDate.AddDays(s.DelayDays) == today)
                        due++;
                }
            }

            return due;
        }

        // ================================================================
        // 3. Letters Printed Today
        // ================================================================
        public async Task<int> GetLettersPrintedTodayAsync()
        {
            var today = DateTime.Today;
            return await _context.PrintHistory
                .Where(p => p.PrintedAt.Date == today)
                .CountAsync();
        }

        // ================================================================
        // 4. Letters Printed This Month
        // ================================================================
        public async Task<int> GetLettersPrintedThisMonthAsync()
        {
            var startOfMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            return await _context.PrintHistory
                .Where(p => p.PrintedAt >= startOfMonth)
                .CountAsync();
        }
    }
}