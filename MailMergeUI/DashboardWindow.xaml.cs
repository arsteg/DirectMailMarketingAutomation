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
using System.Windows;
using System.Windows.Input;

namespace MailMergeUI
{
    public partial class DashboardWindow : Window
    {
        private readonly MailMergeDbContext _dbContext;
        private readonly MailMergeEngine.MailMergeEngine _mailMergeEngine;

        public DashboardWindow(MailMergeDbContext dbContext)
        {
            _dbContext = dbContext;
            InitializeComponent();
            this.Loaded += DashboardWindow_Loaded;
            _mailMergeEngine = App.Services!.GetRequiredService<MailMergeEngine.MailMergeEngine>();
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

        private void Button_Click(object sender, RoutedEventArgs e)
                    {

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

                //// Get the current stage for this campaign
                var stage = campaign.Stages.FirstOrDefault();
                if (stage == null)
                {
                    MessageBox.Show("No stage found for this campaign.", "Error",
                                   MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Get records for this campaign
                var records = await _dbContext.Properties
                    .Where(x => x.CampaignId == campaign.Id)
                    .ToListAsync();

                if (records == null || !records.Any())
                {
                    MessageBox.Show("No records found for this campaign.", "No Records",
                                   MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                //// Get template path
                var templatePath = await _dbContext.Templates
                    .Where(x => x.Id.ToString() == stage.TemplateId)
                    .Select(x => x.Path)
                    .FirstOrDefaultAsync();

                if (string.IsNullOrEmpty(templatePath) || !File.Exists(templatePath))
                {
                    MessageBox.Show("Template file not found.", "Error",
                                   MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Create output directory
                var outputPath = Path.Combine(campaign.OutputPath, stage.StageName);
                if (!Directory.Exists(outputPath))
                {
                    Directory.CreateDirectory(outputPath);
                }

                // Generate output file
                string outputFileName = Path.Combine(outputPath,
                    $"{campaign.Name}_{DateTime.Now:yyyyMMdd_HHmmss}.docx");

                //  // Export the batch
                await _mailMergeEngine.ExportBatch(templatePath, records, outputFileName);

                // Verify the file was created
                if (!File.Exists(outputFileName))
                {
                    Log.Error($"Failed to generate document: {outputFileName}");
                    MessageBox.Show("Failed to generate document.", "Error",
                                   MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                Log.Information($"Document generated: {outputFileName}");

                // Print if printer is configured
                if (!string.IsNullOrWhiteSpace(selectedPrinter) && selectedPrinter != "Select Printer")
                {
                    var printers = System.Drawing.Printing.PrinterSettings.InstalledPrinters;
                    bool printerExists = false;

                    foreach (string printer in printers)
                    {
                        if (printer.Equals(selectedPrinter, StringComparison.OrdinalIgnoreCase))
                        {
                            printerExists = true;
                            break;
                        }
                    }

                    if (printerExists)
                    {
                        // Convert DOCX to PDF
                        string pdfPath = Path.Combine(outputPath,
                            $"{campaign.Name}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");

                        using (WordDocument wordDocument = new WordDocument(outputFileName,
                            Syncfusion.DocIO.FormatType.Automatic))
                        {
                            var converter = new DocToPDFConverter();
                            using (var pdfDocument = converter.ConvertToPDF(wordDocument))
                            {
                                pdfDocument.Save(pdfPath);
                            }
                        }

                        // Print the PDF
                        if (File.Exists(pdfPath))
                        {
                            using (var pdfDoc = PdfiumViewer.PdfDocument.Load(pdfPath))
                            using (var printDoc = pdfDoc.CreatePrintDocument())
                            {
                                printDoc.DocumentName = $"{campaign.Name}_{DateTime.Now:yyyyMMdd_HHmmss}";
                                printDoc.PrinterSettings.PrinterName = selectedPrinter;
                                printDoc.Print();
                                Log.Information("Printed {Count} letters to {Printer} for campaign {Campaign}",
                                    records.Count, selectedPrinter, campaign.Name);
                            }

                            MessageBox.Show($"Successfully printed {records.Count} letters for campaign: {campaign.Name}",
                                "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        else
                        {
                            MessageBox.Show("Failed to convert document to PDF.", "Error",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    else
                    {
                        Log.Warning($"Printer '{selectedPrinter}' not found. Available printers: {string.Join(", ", printers.Cast<string>())}");
                        MessageBox.Show($"Printer '{selectedPrinter}' not found.\n\nAvailable printers:\n{string.Join("\n", printers.Cast<string>())}",
                            "Printer Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                else
                {
                    Log.Information($"No printer configured for campaign '{campaign.Name}'. Document saved to: {outputFileName}");
                    MessageBox.Show($"Document generated successfully (no printer configured).\n\nSaved to:\n{outputFileName}",
                        "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                // Refresh dashboard
                 await viewModel.LoadPendingCountAsync();
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
