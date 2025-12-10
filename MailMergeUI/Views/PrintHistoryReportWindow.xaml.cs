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
            _dbContext = dbContext;
            Loaded += PrintHistoryReportWindow_Loaded;
        }

        private void PrintHistoryReportWindow_Loaded(object sender, RoutedEventArgs e)
        {
            SetDefaultDateRange();
            LoadPrintHistoryReport();
        }

        private void SetDefaultDateRange()
        {
            var today = DateTime.Today;
            bool isLastDayOfMonth = today.AddDays(1).Day == 1; // tomorrow is 1st of next month

            DateTime fromDate = isLastDayOfMonth
                ? new DateTime(today.Year, today.Month, 1)
                : new DateTime(today.AddMonths(-1).Year, today.AddMonths(-1).Month, 1);

            dpFromDate.SelectedDate = fromDate;
            dpToDate.SelectedDate = today;
        }

        private void LoadPrintHistoryReport()
        {
            if (!dpFromDate.SelectedDate.HasValue || !dpToDate.SelectedDate.HasValue)
                return;

            var fromDate = dpFromDate.SelectedDate.Value.Date;
            var toDate = dpToDate.SelectedDate.Value.Date;

            var data = _dbContext.PrintHistory
                .Where(ph => ph.PrintedAt.Date >= fromDate && ph.PrintedAt.Date <= toDate)
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
            lblTotal.Text = total.ToString("N0");

            lblDateRange.Text = $"Showing {data.Count} days from {fromDate:MMMM dd, yyyy} to {toDate:MMMM dd, yyyy} • Total: {total:N0} letters";
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e) => LoadPrintHistoryReport();

        private void BtnApplyFilter_Click(object sender, RoutedEventArgs e)
        {
            if (!dpFromDate.SelectedDate.HasValue || !dpToDate.SelectedDate.HasValue)
            {
                MessageBox.Show("Please select both From and To dates.", "Missing Date", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (dpFromDate.SelectedDate > dpToDate.SelectedDate)
            {
                MessageBox.Show("From Date cannot be later than To Date.", "Invalid Range", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            LoadPrintHistoryReport();
        }
    }

    public class PrintDaySummary
    {
        public DateTime PrintDate { get; set; }
        public int Count { get; set; }
    }
}