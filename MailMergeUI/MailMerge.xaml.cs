using MailMerge.Data.Models;
using Microsoft.Win32;
using PdfiumViewer;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MailMergeUI
{
    public partial class MailMergeWindow : Window
    {
        private string templatePath = string.Empty;
        private string csvPath = string.Empty;
        private List<PropertyRecord> records = new();
        private MailMergeEngine.MailMergeEngine engine = new();
        private bool isDarkMode = false;

        public MailMergeWindow()
        {
            InitializeComponent();
            LoadPrinters();
        }

        private void LoadPrinters()
        {
            cmbPrinters.Items.Clear();
            foreach (string printer in PrinterSettings.InstalledPrinters)
            {
                cmbPrinters.Items.Add(printer);
            }
            if (cmbPrinters.Items.Count > 0)
                cmbPrinters.SelectedIndex = 0;
        }

        private void btnLoadTemplate_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog() { Filter = "PDF Files|*.pdf" };
            if (ofd.ShowDialog() == true)
            {
                templatePath = ofd.FileName;
                Log($"Template loaded: {templatePath}");
            }
        }

        private void btnLoadCsv_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog() { Filter = "CSV Files|*.csv" };

            try
            {
                if (ofd.ShowDialog() == true)
                {
                    ShowLoader();
                    csvPath = ofd.FileName;

                    records = engine.ReadCsv(csvPath);

                    Log($"CSV loaded: {csvPath} ({records.Count} records)");
                }
            }
            catch (Exception ex)
            {
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
                string pdfPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    "MailMergeOutput",
                    "merged_batch_sample.pdf");

                if (!File.Exists(pdfPath))
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

                using (var pdfDoc = PdfiumViewer.PdfDocument.Load(pdfPath))
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
                txtStatus.Text = $"Printing failed: {ex.Message}\n";
            }
        }

        private void Log(string msg)
        {
            txtStatus.Text = txtStatus.Text + Environment.NewLine + Environment.NewLine + msg;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void btnBack_Click(object sender, RoutedEventArgs e)
        {
            DashboardWindow dashboardWindow = new DashboardWindow();
            dashboardWindow.Show();
            this.Close();
        }

        private void btnPreview_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(templatePath) || records.Count == 0)
            {
                Log("Please load template and CSV first.");
                return;
            }
            try
            {
                ShowLoader();

                var pdfBytes = engine.FillTemplate(templatePath, records.First());
                using (var bmp = RenderPdfPreview(pdfBytes))
                using (var msImg = new MemoryStream())
                {
                    bmp.Save(msImg, System.Drawing.Imaging.ImageFormat.Png);
                    msImg.Position = 0;

                    var img = new BitmapImage();
                    img.BeginInit();
                    img.CacheOption = BitmapCacheOption.OnLoad;
                    img.StreamSource = msImg;
                    img.EndInit();
                    img.Freeze();

                    imgPreview.Source = img;
                }

                Log("Preview generated for first record.");
            }
            catch (Exception ex)
            {
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
                    Title = "Save PDF File",
                    Filter = "PDF Document (*.pdf)|*.pdf",
                    FileName = "merged_batch_sample.pdf", // default name
                    DefaultExt = ".pdf"
                };

                bool? result = saveDialog.ShowDialog();

                if (result != true)
                {
                    Log("Export cancelled by user.");
                    return;
                }

                string pdfOut = saveDialog.FileName;

                // Export the PDF
                engine.ExportBatch(templatePath, records, pdfOut);

                Log($"Export complete. File saved at {pdfOut}");
            }
            catch (Exception ex)
            {
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

    }
}
