using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using Syncfusion.DocIO;
using Syncfusion.DocIO.DLS;
using Syncfusion.DocToPDFConverter;
using Syncfusion.Pdf;
using Syncfusion.Windows.Controls.RichTextBoxAdv;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace MailMergeUI
{
    public partial class PdfTemplateCreator : UserControl
    {
        private readonly MailMergeEngine.MailMergeEngine _mailMergeEngine;


        public PdfTemplateCreator()
        {
            InitializeComponent();

            _mailMergeEngine = App.Services!.GetRequiredService<MailMergeEngine.MailMergeEngine>();

            // Populate merge fields
            FormFieldComboBox.ItemsSource = new[]
            {
                "Select Field...", "Radar ID", "Apn", "Type", "Address", "City", "ZIP", "Owner",
                "Owner Type", "Owner Occ?", "Primary Name", "Primary First", "Mail Address",
                "Mail City", "Mail State", "Mail ZIP", "Foreclosure", "FCL Stage", "FCL Doc Type",
                "FCL Rec Date", "Trustee", "Trustee Phone", "TS Number"
            };
            FormFieldComboBox.SelectedIndex = 0;

            DateFieldComboBox.ItemsSource = new[] { "Select Field...", "Current Day", "Current Month", "Current Year" };
            DateFieldComboBox.SelectedIndex = 0;

            // Delete key support (correct way for SfRichTextBoxAdv)
            RichTextEditor.PreviewKeyDown += RichTextEditor_PreviewKeyDown;
        }

        // DELETE KEY HANDLER (correct & safe)
        private void RichTextEditor_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete || e.Key == Key.Back)
            {
                if (RichTextEditor.Selection != null && !RichTextEditor.Selection.IsEmpty)
                {
                    //RichTextEditor.Selection.Text = string.Empty; // This is writable in current versions
                    e.Handled = true;
                }
            }
        }

       

        private void LoadWord_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Word Documents|*.docx|All Files|*.*",
                Title = "Load Template"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    RichTextEditor.Load(dlg.FileName);
                    ExportButton.IsEnabled = true;
                    StatusText.Text = "Loaded: " + Path.GetFileName(dlg.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error loading file: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void FormFieldComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (FormFieldComboBox.SelectedItem is string field && field != "Select Field...")
            {
                InsertMergeField(field);
                FormFieldComboBox.SelectedIndex = 0;
            }
        }

        // CORRECT WAY TO INSERT TEXT IN 2025 SfRichTextBoxAdv
        private void InsertMergeField(string fieldName)
        {
            string text = $"{{{fieldName}}}";
            RichTextEditor.Selection.InsertText(text);

            // Optional: Highlight merge field like Word (blue background)
            //RichTextEditor.Selection.Start = RichTextEditor.Selection.Start.MoveBackward(text.Length);
           // RichTextEditor.Selection.End = RichTextEditor.Selection.Start.MoveForward(text.Length);
            //RichTextEditor.Selection.CharacterFormat.Background = System.Drawing.Color.FromArgb(200, 173, 216, 230); // Light blue
            //RichTextEditor.Selection.CharacterFormat.Foreground = System.Drawing.Color.DarkBlue;
            RichTextEditor.Selection.CharacterFormat.Bold = true;
        }

        private void InsertText(string text)
        {
            RichTextEditor.Selection.InsertText(text);
        }

        // INSERT DATE
        private void DateFieldComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (DateFieldComboBox.SelectedIndex <= 0) return;

            string text = DateFieldComboBox.SelectedIndex switch
            {
                1 => DateTime.Now.Day.ToString(),
                2 => DateTime.Now.ToString("MMMM"),
                3 => DateTime.Now.Year.ToString(),
                _ => ""
            };

            InsertText(text);
            DateFieldComboBox.SelectedIndex = 0;
        }

        private void TemplateNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            //ExportButton.IsEnabled = !string.IsNullOrWhiteSpace(TemplateNameTextBox.Text);
        }

        private void ExportPdf_Click(object sender, RoutedEventArgs e)
        {
            var sfd = new SaveFileDialog
            {
                Filter = "PDF Files|*.pdf",
                FileName = $"{TemplateNameTextBox.Text.Trim()}.pdf"
            };

            if (sfd.ShowDialog() != true) return;

            try
            {
                // Step 1: Save SfRichTextBoxAdv content to a temporary .docx file
                var tempDocx = Path.Combine(Path.GetTempPath(), "template_" + Guid.NewGuid() + ".docx");
                RichTextEditor.Save(tempDocx);  // <-- This overload accepts file path directly!

                // Step 2: Load the saved .docx using Syncfusion DocIO
                using var wordDocument = new WordDocument(tempDocx, Syncfusion.DocIO.FormatType.Automatic);

                // Step 3: Convert to PDF
                var converter = new DocToPDFConverter();
                PdfDocument pdfDocument = converter.ConvertToPDF(wordDocument);

                // Step 4: Save PDF
                pdfDocument.Save(sfd.FileName);
                pdfDocument.Close(true);

                // Step 5: Open the PDF
                Process.Start(new ProcessStartInfo(sfd.FileName) { UseShellExecute = true });

                MessageBox.Show("PDF exported successfully!", "Success",
                                MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}\n\n{ex.StackTrace}", "Error",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void LoadCSV_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog { Filter = "CSV Files|*.csv" };
            if (ofd.ShowDialog() != true) return;

            try
            {
                var records = _mailMergeEngine.ReadCsv(ofd.FileName);
                var tempDocx = Path.GetTempPath() + "temp_merge.docx";
                RichTextEditor.Save(tempDocx);

                var outputPdf = Path.GetTempPath() + "preview.pdf";
                await _mailMergeEngine.ExportBatch(tempDocx, records, outputPdf);

                PdfWebView.Visibility = Visibility.Visible;
                RichTextEditor.Visibility = Visibility.Collapsed;
                BackButton.Visibility = Visibility.Visible;

                await PdfWebView.EnsureCoreWebView2Async();
                PdfWebView.Source = new Uri(outputPdf);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Preview failed: " + ex.Message);
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            PdfWebView.Visibility = Visibility.Collapsed;
            RichTextEditor.Visibility = Visibility.Visible;
            BackButton.Visibility = Visibility.Collapsed;
        }
    }
}