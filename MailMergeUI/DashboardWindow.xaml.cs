using MailMerge.Data;
using MailMerge.Data.Models;
using MailMergeUI.ViewModels;
using MailMergeUI.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Syncfusion.DocIO.DLS;
using Syncfusion.DocToPDFConverter;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Policy;
using System.Windows;
using System.Windows.Input;

namespace MailMergeUI
{
    public partial class DashboardWindow : Window
    {
        private readonly MailMergeDbContext _dbContext;
        private readonly MailMergeEngine.MailMergeEngine _mailMergeEngine;
        private readonly ApiService _apiService;

        public DashboardWindow(MailMergeDbContext dbContext)
        {
            _dbContext = dbContext;
            InitializeComponent();
            this.Loaded += DashboardWindow_Loaded;
            _mailMergeEngine = App.Services!.GetRequiredService<MailMergeEngine.MailMergeEngine>();
            _apiService = new ApiService(_mailMergeEngine, dbContext);

        }

        private void DashboardWindow_Loaded(object sender, RoutedEventArgs e)
        {
            this.DataContext = new MainWindowViewModel(_dbContext);
        }

        private void OpenMainWindow_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                MailMergeWindow main = new MailMergeWindow(_dbContext);
                main.WindowState = this.WindowState;
                main.Show();
                this.Close();
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to open MailMergeWindow");
                MessageBox.Show($"Error opening Mail Merge: {ex.Message}", "Error");
            }
        }

        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SettingsWindow settings = new SettingsWindow(_dbContext);
                settings.WindowState = this.WindowState;
                settings.Show();
                this.Close();
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to open SettingsWindow");
                MessageBox.Show($"Error opening Settings: {ex.Message}", "Error");
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void btnLogout_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.Username = string.Empty;
            Properties.Settings.Default.RememberMe = false;
            Properties.Settings.Default.Save();

            LoginWindow loginWindow = new LoginWindow(_dbContext);
            loginWindow.Show();
            this.Close();
        }

        private void btnTemplate_Click(object sender, RoutedEventArgs e)
        {
            TemplateListView template = new TemplateListView(_dbContext);
            template.WindowState = this.WindowState;
            template.Show();
            this.Close();
        }

        private void Grid_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void btnShowBlacklisted_Click(object sender, RoutedEventArgs e)
        {
            BlacklistView blacklistView = new BlacklistView(this._dbContext);
            blacklistView.WindowState = this.WindowState;
            blacklistView.WindowStartupLocation = this.WindowStartupLocation;
            blacklistView.Show();
            this.Close();
        }

        private void btnCampaign_Click(object sender, RoutedEventArgs e)
        {
            CampaignListView campaignListView = new CampaignListView(this._dbContext);
            campaignListView.WindowState = this.WindowState;
            campaignListView.WindowStartupLocation = this.WindowStartupLocation;
            campaignListView.Show();
            this.Close();
        }

        private void btnCreateTemplate_Click(object sender, RoutedEventArgs e)
        {
            TemplateWindow template = new TemplateWindow(_dbContext);
            template.WindowState = this.WindowState;
            template.Show();
            this.Close();
        }

        private void btnPrintHistory_Click(object sender, RoutedEventArgs e)
        {
            PrintHistoryReportWindow campaignListView = new PrintHistoryReportWindow(this._dbContext);
            campaignListView.WindowState = this.WindowState;
            campaignListView.WindowStartupLocation = this.WindowStartupLocation;
            campaignListView.Show();

        }

        private async void btnPrintTodaysBatch_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Get the selected campaign from the ViewModel
                var viewModel = this.DataContext as MainWindowViewModel;

                if (viewModel?.ActiveCampaign == null)
                {
                    MessageBox.Show("Please select a campaign first.", "No Campaign Selected",
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var campaign = viewModel.ActiveCampaign;
                var selectedPrinter = campaign.Printer;
                var runAt = campaign.LeadSource.RunAt;
                var scheduleType = campaign.LeadSource.Type;
                var daysOfWeek = campaign.LeadSource.DaysOfWeek;

                if (scheduleType == ScheduleType.Daily)
                {
                    var nowTime = DateTime.Now;
                    if (nowTime.TimeOfDay < runAt)
                    {
                        MessageBox.Show("No Batch is available to Print", "Not Available",
                                       MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                    if (nowTime.TimeOfDay >= runAt)
                    {
                        foreach (var stage in campaign.Stages)
                        {

                            try
                            {
                                if (DateTime.Now >= campaign.LastRunningTime.AddDays(stage.DelayDays))
                                {
                                    Log.Information("Processing Stage: {StageName} for Campaign: {CampaignName}", stage.StageName, campaign.Name);
                                    await viewModel.LoadPendingCountAsync();
                                    var records = await _dbContext.Properties.Where(x => x.CampaignId == campaign.Id && x.IsBlackListed == false).ToListAsync();
                                    var templatePath = await _dbContext.Templates.Where(x => x.Id.ToString() == stage.TemplateId).Select(x => x.Path).FirstOrDefaultAsync();
                                    var outputPath = Path.Combine(campaign.OutputPath, stage.StageName);
                                    string outputFileName = Path.Combine(outputPath, $"{campaign.Name}.docx");
                                    string pdfFileName = Path.Combine(outputPath, $"{campaign.Name}.pdf");

                                    if (!File.Exists(outputFileName))
                                    {
                                        Directory.CreateDirectory(outputPath);
                                        if (templatePath != null)
                                        {
                                            await _mailMergeEngine.ExportBatch(templatePath, records, Path.Combine(outputPath, $"{campaign.Name}.docx"));

                                            // Convert DOCX to PDF
                                            
                                            using (WordDocument wordDocument = new WordDocument(outputFileName, Syncfusion.DocIO.FormatType.Automatic))
                                            {
                                                var converter = new DocToPDFConverter();
                                                using (var pdfDocument = converter.ConvertToPDF(wordDocument))
                                                {
                                                    pdfDocument.Save(pdfFileName);  // ✅ Save PDF to .pdf file
                                                }
                                            }

                                            
                                         
                                        }
                                    }

                                    using (var pdfDoc = PdfiumViewer.PdfDocument.Load(pdfFileName))
                                    using (var printDoc = pdfDoc.CreatePrintDocument())
                                    {
                                        printDoc.DocumentName = "MailMerge Output";
                                        printDoc.PrinterSettings.PrinterName = selectedPrinter;
                                        printDoc.Print();
                                    }

                                    MessageBox.Show($"Successfully printed {records.Count} letters for campaign: {campaign.Name}",
                        "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                                    stage.IsRun = true;
                                }
                            }
                            catch (Exception ex)
                            {
                                Log.Error(ex, "Error processing stage {StageName} for campaign {CampaignName}", stage.StageName, campaign.Name);
                                // throw; // Don't crash the loop
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
                        var nowTime = DateTime.Now;
                        if (nowTime.TimeOfDay < runAt)
                        {
                            MessageBox.Show("No Batch is available to Print", "Not Available",
                                           MessageBoxButton.OK, MessageBoxImage.Information);
                            return;
                        }
                        if (nowTime.TimeOfDay >= runAt)
                        {
                            foreach (var stage in campaign.Stages)
                            {

                                try
                                {
                                    if (DateTime.Now >= campaign.LastRunningTime.AddDays(stage.DelayDays))
                                    {
                                        Log.Information("Processing Stage: {StageName} for Campaign: {CampaignName}", stage.StageName, campaign.Name);
                                        await viewModel.LoadPendingCountAsync();
                                        var records = await _dbContext.Properties.Where(x => x.CampaignId == campaign.Id && x.IsBlackListed == false).ToListAsync();
                                        var templatePath = await _dbContext.Templates.Where(x => x.Id.ToString() == stage.TemplateId).Select(x => x.Path).FirstOrDefaultAsync();
                                        var outputPath = Path.Combine(campaign.OutputPath, stage.StageName);
                                        string outputFileName = Path.Combine(outputPath, $"{campaign.Name}.docx");
                                        string pdfFileName = Path.Combine(outputPath, $"{campaign.Name}.pdf");

                                        if (!File.Exists(outputFileName))
                                        {
                                            Directory.CreateDirectory(outputPath);
                                            if (templatePath != null)
                                            {
                                                await _mailMergeEngine.ExportBatch(templatePath, records, Path.Combine(outputPath, $"{campaign.Name}.docx"));

                                                // Convert DOCX to PDF

                                                using (WordDocument wordDocument = new WordDocument(outputFileName, Syncfusion.DocIO.FormatType.Automatic))
                                                {
                                                    var converter = new DocToPDFConverter();
                                                    using (var pdfDocument = converter.ConvertToPDF(wordDocument))
                                                    {
                                                        pdfDocument.Save(pdfFileName);  // ✅ Save PDF to .pdf file
                                                    }
                                                }



                                            }
                                        }

                                        using (var pdfDoc = PdfiumViewer.PdfDocument.Load(pdfFileName))
                                        using (var printDoc = pdfDoc.CreatePrintDocument())
                                        {
                                            printDoc.DocumentName = "MailMerge Output";
                                            printDoc.PrinterSettings.PrinterName = selectedPrinter;
                                            printDoc.Print();
                                        }
                                        MessageBox.Show($"Successfully printed {records.Count} letters for campaign: {campaign.Name}",
                        "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                                        stage.IsRun = true;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Log.Error(ex, "Error processing stage {StageName} for campaign {CampaignName}", stage.StageName, campaign.Name);
                                    // throw; // Don't crash the loop
                                }


                            }
                        }
                    }
                }

            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error printing batch for selected campaign");
                MessageBox.Show($"Error printing batch:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
