using MailMerge.Data;
using MailMergeEngine;
using Microsoft.Win32;
using PdfiumViewer;
using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;

namespace MailMergeUI
{
    public partial class MainWindow : Window
    {
        private string templatePath = string.Empty;
        private string csvPath = string.Empty;
        private List<Lead> records = new();
        private MailMergeEngine.AddressRightMailMergeEngine arEngine = new();
        private RisoFtMailMergeEngine.RisoFtMailMergeEngine rfEngine = new();

        public MainWindow()
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
            var ofd = new OpenFileDialog() { Filter = "Image Files|*.png;*.jpg;*.bmp" };
            if (ofd.ShowDialog() == true)
            {
                templatePath = ofd.FileName;
                Log($"Template loaded: {templatePath}");
            }
        }

        private void btnLoadCsv_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog() { Filter = "CSV Files|*.csv" };
            if (ofd.ShowDialog() == true)
            {
                csvPath = ofd.FileName;
                records = arEngine.ReadCsv(csvPath);
                Log($"CSV loaded: {csvPath} ({records.Count} records)");
            }
        }

        private void btnPreview_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(templatePath) || records.Count == 0)
            {
                Log("Please load template and CSV first.");
                return;
            }

            string tmpFile = Path.Combine(Path.GetTempPath(), "preview.png");
            arEngine.MergeToPng(templatePath, records.First(), tmpFile);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri(tmpFile);
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            imgPreview.Source = bmp;

            Log("Preview generated for first record.");
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

                using (var pdfDoc = PdfDocument.Load(pdfPath))
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

        private void btnPrintRiso_Click(object sender, RoutedEventArgs e)
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

                using (var pdfDoc = PdfDocument.Load(pdfPath))
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


        private void btnExport_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(templatePath) || records.Count == 0)
            {
                Log("Please load template and CSV first.");
                return;
            }

            string outDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "MailMergeOutput");
            Directory.CreateDirectory(outDir);

            int i = 1;
            foreach (var r in records)
            {
                string outPath = Path.Combine(outDir, $"merged_{i:00}_{r.FirstName}.png");
                arEngine.MergeToPng(templatePath, r, outPath);
                i++;
            }
            string pdfOut = Path.Combine(outDir, "merged_batch_sample.pdf");
            arEngine.CombinePngsToPdf(outDir, pdfOut);

            Log($"Export complete. Files in {outDir}");
        }

        private void Log(string msg)
        {
            txtStatus.Text = msg + Environment.NewLine;
            
        }
    }
}
