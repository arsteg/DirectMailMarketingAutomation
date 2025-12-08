using MailMerge.Data;
using MailMerge.Data.Models;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using Syncfusion.DocIO.DLS;
using Syncfusion.DocToPDFConverter;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace MailMergeUI
{
    public partial class MailMergeWindow : Window
    {
        private string templatePath = string.Empty;
        private string csvPath = string.Empty;
        private string exportPath = string.Empty;
        private List<PropertyRecord> records = new();
        private MailMergeEngine.MailMergeEngine engine;
        private bool isDarkMode = false;
        private string lastTempPdfPath = string.Empty; // store last preview temp file to clean up later
        private readonly MailMergeDbContext _dbContext;

        public MailMergeWindow(MailMergeDbContext dbContext)
        {
            _dbContext = dbContext;
            engine = new MailMergeEngine.MailMergeEngine(dbContext);
            InitializeComponent();
            LoadPrinters();
        }

        private void LoadPrinters()
        {
            cmbPrinters.Items.Clear();
            foreach (string printer in System.Drawing.Printing.PrinterSettings.InstalledPrinters)
            {
                cmbPrinters.Items.Add(printer);
            }
            if (cmbPrinters.Items.Count > 0)
                cmbPrinters.SelectedIndex = 0;
        }

        private async void btnLoadTemplate_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog() { Filter = "Word Documents|*.docx|All Files|*.*" };
            if (ofd.ShowDialog() == true)
            {
                templatePath = ofd.FileName;

                // Choose a folder you have access to, e.g., in AppData
                string userDataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "MailMax"
                );

                // this asynchronously ensures the CoreWebView2 is ready
                //await PdfWebView.EnsureCoreWebView2Async();
                var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
                await PdfWebView.EnsureCoreWebView2Async(env);

                WordDocument wordDocument = new WordDocument(ofd.FileName,Syncfusion.DocIO.FormatType.Automatic);
                var converter = new DocToPDFConverter();
                var pdfDocument = converter.ConvertToPDF(wordDocument);

                string tempFile = Path.Combine(Path.GetTempPath(), $"mailmerge_preview_{Guid.NewGuid()}.pdf");
                pdfDocument.Save(tempFile);
                pdfDocument.Close(true);

                // navigate to the temp file
                PdfWebView.CoreWebView2.Navigate(new Uri(tempFile).AbsoluteUri);
                Log($"Template loaded: {templatePath}");
            }
        }

        private async void btnLoadCsv_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog() { Filter = "CSV Files|*.csv" };

            try
            {
                if (ofd.ShowDialog() == true)
                {
                    ShowLoader();
                    csvPath = ofd.FileName;

                    records = engine.ReadCsv(csvPath);

                    ShowPreview();

                    Log($"CSV loaded: {csvPath} ({records.Count} records)");
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Error loading CSV: {Path}", csvPath);
                Log($"ERROR: {ex.Message}");
            }
            finally
            {
                HideLoader();
            }
        }

        private void btnPrint_Click(object sender, RoutedEventArgs e)
        {
            try
            {

                if (!File.Exists(exportPath))
                {
                    txtStatus.Text = "Error: No merged PDF found. Export first.\n";
                    return;
                }

                if (cmbPrinters.SelectedItem == null)
                {
                    txtStatus.Text = "Error: No printer selected.\n";
                    return;
                }

                string selectedPrinter = cmbPrinters.SelectedItem.ToString()!;

                using (var pdfDoc = PdfiumViewer.PdfDocument.Load(exportPath))
                using (var printDoc = pdfDoc.CreatePrintDocument())
                {
                    printDoc.DocumentName = "MailMerge Output";
                    printDoc.PrinterSettings.PrinterName = selectedPrinter;
                    printDoc.Print();
                }

                txtStatus.Text = $"Print job sent to {selectedPrinter}\n";
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Printing failed");
                txtStatus.Text = $"Printing failed: {ex.Message}\n";
            }
        }

        private void Log(string msg)
        {
            Serilog.Log.Debug("[UI Log] {Message}", msg);
            txtStatus.Text = txtStatus.Text + Environment.NewLine + Environment.NewLine + msg;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void btnBack_Click(object sender, RoutedEventArgs e)
        {
            DashboardWindow dashboardWindow = new DashboardWindow(_dbContext);
            dashboardWindow.WindowState = this.WindowState;
            dashboardWindow.Show();
            this.Close();
        }

        private async void ShowPreview()
        {
            if (string.IsNullOrEmpty(templatePath) || records.Count == 0)
            {
                Log("Please load template and CSV first.");
                return;
            }

            try
            {
                ShowLoader();

                // write to a unique temp file`
                string wordTempFile = Path.Combine(Path.GetTempPath(), $"mailmerge_preview_{Guid.NewGuid()}.docx");

                await engine.ExportBatch(templatePath, records, wordTempFile);
                WordDocument wordDocument = new WordDocument(wordTempFile, Syncfusion.DocIO.FormatType.Automatic);
                var converter = new DocToPDFConverter();
                var pdfDocument = converter.ConvertToPDF(wordDocument);

                string tempFile = Path.Combine(Path.GetTempPath(), $"mailmerge_preview_{Guid.NewGuid()}.pdf");
                pdfDocument.Save(tempFile);
                pdfDocument.Close(true);

                // Keep track so we can optionally delete later
                lastTempPdfPath = tempFile;

                // Ensure WebView2 is initialized
                try
                {
                    // this asynchronously ensures the CoreWebView2 is ready
                    await PdfWebView.EnsureCoreWebView2Async();
                    // navigate to the temp file
                    PdfWebView.CoreWebView2.Navigate(new Uri(tempFile).AbsoluteUri);
                    Log("Preview generated.");
                }
                catch (Exception webviewEx)
                {
                    // If WebView2 failed to initialize (runtime missing), fallback to launching external viewer
                    Log($"WebView2 initialization failed: {webviewEx.Message}. Opening external PDF viewer as fallback.");
                    try
                    {
                        ProcessStartInfo psi = new ProcessStartInfo
                        {
                            FileName = tempFile,
                            UseShellExecute = true
                        };
                        Process.Start(psi);
                    }
                    catch (Exception ex2)
                    {
                        Log($"Failed to open PDF externally: {ex2.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Error showing preview");
                Log($"ERROR: {ex.Message}");
            }
            finally
            {
                HideLoader();
            }
        }

        private void btnExport_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(templatePath) || records.Count == 0)
            {
                Log("Please load template and CSV first.");
                return;
            }

            try
            {
                ShowLoader();

                // Let user choose where to save the PDF
                var saveDialog = new SaveFileDialog
                {
                    Title = "Save Word Document",
                    Filter = "Word Document (*.docx)|*.docx",
                    FileName = "merged_batch_sample.docx", // default name
                    DefaultExt = ".docx"
                };

                bool? result = saveDialog.ShowDialog();

                if (result != true)
                {
                    Log("Export cancelled by user.");
                    return;
                }

                exportPath = saveDialog.FileName;

                // Export the PDF
                engine.ExportBatch(templatePath, records, exportPath);

                Log($"Export complete. File saved at {exportPath}");
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Error exporting batch");
                Log($"ERROR: {ex.Message}");
            }
            finally
            {
                HideLoader();
            }
        }

        public Bitmap RenderPdfPreview(byte[] pdfBytes, int dpi = 300)
        {
            using (var pdfDoc = PdfiumViewer.PdfDocument.Load(new MemoryStream(pdfBytes)))
            {
                var size = pdfDoc.PageSizes[0];
                int widthPx = (int)(size.Width / 72f * dpi);
                int heightPx = (int)(size.Height / 72f * dpi);

                return (Bitmap)pdfDoc.Render(0, widthPx, heightPx, dpi, dpi, forPrinting: true);
            }
        }

        private void ShowLoader()
        {
            Dispatcher.Invoke(() =>
            {
                LoaderOverlay.Visibility = Visibility.Visible;
                MainGrid.IsEnabled = false; // Disable interaction with other controls
            });
        }

        private void HideLoader()
        {
            Dispatcher.Invoke(() =>
            {
                LoaderOverlay.Visibility = Visibility.Collapsed;
                MainGrid.IsEnabled = true; // Re-enable interaction
            });
        }

        private void MainGrid_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }
    }
}
