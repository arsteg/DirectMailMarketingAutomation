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
        private SfRichTextBoxAdv _richTextEditor;

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
            _richTextEditor = RichTextEditor;
            InitializeFormatting();
        }

        private void InitializeFormatting()
        {
            // Set default font size
            FontSizeComboBox.SelectedIndex = 3; // 14pt
            FontFamilyComboBox.SelectedItem = "Calibri";
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

             

        #region Text Formatting - Bold, Italic, Underline

        private void Bold_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                RichTextEditor.Selection.CharacterFormat.Bold = !RichTextEditor.Selection.CharacterFormat.Bold;
                _richTextEditor.Focus();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error applying bold: {ex.Message}", "Error");
            }
        }

        private void Italic_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                RichTextEditor.Selection.CharacterFormat.Italic = !RichTextEditor.Selection.CharacterFormat.Italic;
                _richTextEditor.Focus();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error applying italic: {ex.Message}", "Error");
            }
        }

        private void Underline_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (UnderlineButton.IsChecked == true)
                {
                    RichTextEditor.Selection.CharacterFormat.Underline = Underline.Single;
                }
                else
                {
                    RichTextEditor.Selection.CharacterFormat.Underline = Underline.None;
                }
                _richTextEditor.Focus();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error applying underline: {ex.Message}", "Error");
            }
        }

        #endregion

        #region Font Formatting

        private void FontSize_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            try
            {
                if (FontSizeComboBox.SelectedItem != null)
                {
                    if (double.TryParse(FontSizeComboBox.SelectedValue.ToString().Split(":")[0], out double fontSize))
                    {
                        _richTextEditor.Selection.CharacterFormat.FontSize =  fontSize;
                        _richTextEditor.Focus();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error changing font size: {ex.Message}", "Error");
            }
        }

        private void FontFamily_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            try
            {
                if (FontFamilyComboBox.SelectedItem != null)
                {
                    string? fontFamily = FontFamilyComboBox.SelectedValue?.ToString()?.Split(":")[1];
                    ;
                    _richTextEditor.Selection.CharacterFormat.FontFamily =  (new FontFamily(fontFamily));
                    _richTextEditor.Focus();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error changing font family: {ex.Message}", "Error");
            }
        }



        #endregion

        #region Color and Highlight

        private void TextColor_Click(object sender, RoutedEventArgs e)
        {
            var colorDialog = new System.Windows.Forms.ColorDialog();
            if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                // Convert System.Drawing.Color to WPF Color
                var drawingColor = colorDialog.Color;
                var wpfColor = Color.FromArgb(drawingColor.A, drawingColor.R, drawingColor.G, drawingColor.B);

                // Apply to selected text
                if (_richTextEditor.Selection != null)
                {
                    _richTextEditor.Selection.CharacterFormat.FontColor = wpfColor;
                }

                _richTextEditor.Focus();
            }
        }

        private void HighlightColor_Click(object sender, RoutedEventArgs e)
        {
            var colorDialog = new System.Windows.Forms.ColorDialog();
            if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                // Convert System.Drawing.Color to Syncfusion HighlightColor enum
                var drawingColor = colorDialog.Color;

                HighlightColor highlightColor = HighlightColor.NoColor; // default

                // Try to match the closest Syncfusion HighlightColor
                if (drawingColor == System.Drawing.Color.Yellow)
                    highlightColor = HighlightColor.Yellow;
                else if (drawingColor == System.Drawing.Color.Green)
                    highlightColor = HighlightColor.Green;
                else if (drawingColor == System.Drawing.Color.Blue)
                    highlightColor = HighlightColor.Blue;
                else if (drawingColor == System.Drawing.Color.Red)
                    highlightColor = HighlightColor.Red;
                else if (drawingColor == System.Drawing.Color.Pink)
                    highlightColor = HighlightColor.Pink;
                else if (drawingColor == System.Drawing.Color.Turquoise)
                    highlightColor = HighlightColor.Turquoise;
                else if (drawingColor == System.Drawing.Color.Orange)
                    highlightColor = HighlightColor.BrightGreen;
                else if (drawingColor == System.Drawing.Color.DarkBlue)
                    highlightColor = HighlightColor.DarkBlue;
                else if (drawingColor == System.Drawing.Color.DarkRed)
                    highlightColor = HighlightColor.DarkRed;
                else
                    highlightColor = HighlightColor.Yellow; // fallback

                // Apply to selected text
                if (RichTextEditor.Selection != null)
                {
                    _richTextEditor.Selection.CharacterFormat.HighlightColor = highlightColor;
                }

                RichTextEditor.Focus();
            }
        }

        #endregion

        #region Alignment

        private void AlignLeft_Click(object sender, RoutedEventArgs e)
        {
            if (_richTextEditor.Selection != null)
            {
                _richTextEditor.Selection.ParagraphFormat.TextAlignment = TextAlignment.Left;
            }

            _richTextEditor.Focus();
        }

        private void AlignCenter_Click(object sender, RoutedEventArgs e)
        {
            if (_richTextEditor.Selection != null)
            {
                _richTextEditor.Selection.ParagraphFormat.TextAlignment = TextAlignment.Center;
            }

            _richTextEditor.Focus();
        }

        private void AlignRight_Click(object sender, RoutedEventArgs e)
        {
            if (_richTextEditor.Selection != null)
            {
                _richTextEditor.Selection.ParagraphFormat.TextAlignment = TextAlignment.Right;
            }

            _richTextEditor.Focus();
        }

        #endregion

        #region Lists

        private void BulletList_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                //_richTextEditor.ApplyBullets(FormatType.Docx, ListStyle.Disc);
                _richTextEditor.Focus();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error applying bullet list: {ex.Message}", "Error");
            }
        }

        private void NumberList_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                //_richTextEditor.ApplyNumbering(FormatType.Docx, ListStyle.Decimal);
                _richTextEditor.Focus();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error applying numbered list: {ex.Message}", "Error");
            }
        }

        #endregion

        #region Clear Formatting and Utility

        private void ClearFormat_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Clears ALL formatting (character + paragraph) from the current selection
                if (RichTextEditor.Selection != null && !RichTextEditor.Selection.IsEmpty)
                {
                    RichTextEditor.Selection.CharacterFormat.Bold=false;
                    RichTextEditor.Selection.CharacterFormat.Italic = false;
                    RichTextEditor.Selection.CharacterFormat.Underline = Underline.None;
                }

                BoldButton.IsChecked = false;
                ItalicButton.IsChecked = false;
                UnderlineButton.IsChecked = false;
                FontSizeComboBox.SelectedIndex = 3; // Reset to 14pt
                FontFamilyComboBox.SelectedItem = "Calibri";
                _richTextEditor.Focus();
                UpdateStatus("Formatting cleared");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error clearing formatting: {ex.Message}", "Error");
            }
        }

        #endregion

        #region Helper Methods

        private void UpdateStatus(string message)
        {
            StatusText.Text = $"✓ {message}";
        }

        #endregion


    }
}