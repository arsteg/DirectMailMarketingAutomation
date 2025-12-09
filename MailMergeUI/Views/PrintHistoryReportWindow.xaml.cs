using MailMerge.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Windows;

namespace MailMergeUI.Views
{
    public partial class PrintHistoryReportWindow : Window
    {
        private readonly MailMergeDbContext _dbContext;

        public PrintHistoryReportWindow(MailMergeDbContext dbContext)
        {
            InitializeComponent();
            _dbContext = dbContext; // Or inject via constructor
            Loaded += PrintHistoryReportWindow_Loaded;
        }

        private void PrintHistoryReportWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadPrintHistoryReport();
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadPrintHistoryReport();
        }

        private void LoadPrintHistoryReport()
        {
            var today = DateTime.Today;
            DateTime startDate;
            DateTime endDate = today;

            // Special rule: If today is the last day of the month → show only current month
            bool isLastDayOfMonth = today.AddDays(1).Month != today.Month;

            if (isLastDayOfMonth)
            {
                startDate = new DateTime(today.Year, today.Month, 1);
            }
            else
            {
                // Previous month start → today
                startDate = new DateTime(today.AddMonths(-1).Year, today.AddMonths(-1).Month, 1);
            }

            var data = _dbContext.PrintHistory
                .Where(ph => ph.PrintedAt.Date >= startDate && ph.PrintedAt.Date <= endDate)
                .GroupBy(ph => ph.PrintedAt.Date)
                .Select(g => new PrintDaySummary
                {
                    PrintDate = g.Key,
                    Count = g.Count()
                })
                .OrderBy(x => x.PrintDate)
                .ToList();

            dgPrintHistory.ItemsSource = data;

            int total = data.Sum(x => x.Count);
            lblTotal.Text = total.ToString("N0"); // e.g., 1,234

            lblDateRange.Text = $"Showing data from {startDate:MMMM dd, yyyy} to {endDate:MMMM dd, yyyy} " +
                                (isLastDayOfMonth ? "(Current month only - last day)" : "(Previous month + current)");
        }
    }

    // Helper class for DataGrid
    public class PrintDaySummary
    {
        public DateTime PrintDate { get; set; }
        public int Count { get; set; }
    }
}