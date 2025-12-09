using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using Syncfusion.DocIO.DLS;
using Syncfusion.Windows.Controls.RichTextBoxAdv;
using System;
using System.ComponentModel;
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
        private string _currentDocumentPath;
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
                    _currentDocumentPath = dlg.FileName;
                    RichTextEditor.Load(dlg.FileName);
                    ExportButton.IsEnabled = true;
                    StatusText.Text = "Loaded: " + Path.GetFileName(dlg.FileName);
                }
                catch (Exception ex)
                {
                    Serilog.Log.Error(ex, "Error loading Word document: {Path}", dlg.FileName);
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
            RichTextEditor.Selection.CharacterFormat.Bold = true;
            RichTextEditor.Selection.InsertText(text);

            // Optional: Highlight merge field like Word (blue background)
            //RichTextEditor.Selection.Start = RichTextEditor.Selection.Start.MoveBackward(text.Length);
           // RichTextEditor.Selection.End = RichTextEditor.Selection.Start.MoveForward(text.Length);
            //RichTextEditor.Selection.CharacterFormat.Background = System.Drawing.Color.FromArgb(200, 173, 216, 230); // Light blue
            //RichTextEditor.Selection.CharacterFormat.Foreground = System.Drawing.Color.DarkBlue;
            
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

        private async void ExportPdf_Click(object sender, RoutedEventArgs e)
        {
            // 1. Save source document if requested
            if (SaveSourceCheckBox.IsChecked == true && !string.IsNullOrEmpty(_currentDocumentPath))
            {
                try
                {
                    RichTextEditor.Save(_currentDocumentPath);
                    StatusText.Text = "Source saved: " + Path.GetFileName(_currentDocumentPath);
                }
                catch (Exception ex)
                {
                    Serilog.Log.Warning(ex, "Error saving source document: {Path}", _currentDocumentPath);
                    MessageBox.Show($"Error saving source document: {ex.Message}", "Save Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    // Don't block export — user might still want to export
                }
            }

            // 2. Ask where to save the exported .docx
            var sfd = new SaveFileDialog
            {
                Filter = "Word Document (*.docx)|*.docx|All Files (*.*)|*.*",
                FileName = $"{TemplateNameTextBox.Text.Trim()}.docx",
                Title = "Export Template As..."
            };

            if (sfd.ShowDialog() != true)
                return;

            string finalDocxPath = sfd.FileName;

            try
            {
                // Temporary file for conversion
                string tempDocx = Path.Combine(Path.GetTempPath(), "temp_export_" + Guid.NewGuid() + ".docx");

                // Save current editor content as .docx
                RichTextEditor.Save(tempDocx);

                // Convert to final .docx using DocIO
                using (var wordDocument = new WordDocument(tempDocx))
                {
                    wordDocument.Save(finalDocxPath, Syncfusion.DocIO.FormatType.Docx);
                }

                // Clean up temp file
                if (File.Exists(tempDocx))
                    File.Delete(tempDocx);

                // Success: Open the exported file
                //try
                //{
                //    Process.Start(new ProcessStartInfo(finalDocxPath) { UseShellExecute = true });
                //}
                //catch (Exception ex)
                //{
                //    Serilog.Log.Warning(ex, "Could not open exported file: {Path}", finalDocxPath);
                //}

                MessageBox.Show("Template exported successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                // Only now — after successful export — save to database
                var template = new MailMerge.Data.Models.Template
                {
                    Name = TemplateNameTextBox.Text.Trim(),
                    Path = finalDocxPath
                };

                var (success, message) = await _mailMergeEngine.SaveTemplate(template);

                if (success)
                {
                    StatusText.Text = $"Exported and saved: {template.Name}";
                }
                else
                {
                    MessageBox.Show($"Template exported, but failed to save to database:\n{message}",
                        "Partial Success", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Export failed for template: {Name}", TemplateNameTextBox.Text);
                MessageBox.Show($"Export failed: {ex.Message}", "Error",
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

                var outputPdf = Path.GetTempPath() + "preview.docx";
                await _mailMergeEngine.ExportBatch(tempDocx, records, outputPdf);

                PdfWebView.Visibility = Visibility.Visible;
                RichTextEditor.Visibility = Visibility.Collapsed;
                BackButton.Visibility = Visibility.Visible;

                await PdfWebView.EnsureCoreWebView2Async();
                PdfWebView.Source = new Uri(outputPdf);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Preview failed for CSV: {Path}", ofd.FileName);
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
                // Avoid crash when editor is not yet created
                if (_richTextEditor == null)
                    return;

                if (FontSizeComboBox.SelectedItem is ComboBoxItem item &&
                    double.TryParse(item.Content.ToString(), out double fontSize))
                {
                    _richTextEditor.Selection.CharacterFormat.FontSize = fontSize;
                    _richTextEditor.Focus();
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
                // Create a list with bullet style
                ListAdv listAdv = new ListAdv();
                AbstractListAdv abstractList = new AbstractListAdv();

                ListLevelAdv listLevel = new ListLevelAdv();
                listLevel.ListLevelPattern = ListLevelPattern.Bullet;
                listLevel.NumberFormat = "●";
                listLevel.FollowCharacter = (Syncfusion.Windows.Controls.RichTextBoxAdv.FollowCharacterType)Syncfusion.DocIO.DLS.FollowCharacterType.Tab;
                listLevel.StartAt = 1;
                listLevel.RestartLevel = 0;
                abstractList.Levels.Add(listLevel);
                listAdv.AbstractList = abstractList;

                // Add to document
                _richTextEditor.Document.AbstractLists.Add(abstractList);
                _richTextEditor.Document.Lists.Add(listAdv);

                // Apply to selection using SetList method
                _richTextEditor.Selection.ParagraphFormat.SetList(listAdv);
                _richTextEditor.Selection.ParagraphFormat.ListLevelNumber = 0;

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
      
                // Create a list with numbering style
                ListAdv listAdv = new ListAdv();
                AbstractListAdv abstractList = new AbstractListAdv();

                ListLevelAdv listLevel = new ListLevelAdv();
                listLevel.ListLevelPattern = ListLevelPattern.Arabic; // For 1, 2, 3...
                listLevel.NumberFormat = "%1."; // Format: 1. 2. 3.
                listLevel.FollowCharacter = (Syncfusion.Windows.Controls.RichTextBoxAdv.FollowCharacterType)Syncfusion.DocIO.DLS.FollowCharacterType.Tab;
                listLevel.StartAt = 1;
                listLevel.RestartLevel = 0;

                abstractList.Levels.Add(listLevel);
                listAdv.AbstractList = abstractList;

                // Add to document
                _richTextEditor.Document.AbstractLists.Add(abstractList);
                _richTextEditor.Document.Lists.Add(listAdv);

                // Apply to selection using SetList method
                _richTextEditor.Selection.ParagraphFormat.SetList(listAdv);
                _richTextEditor.Selection.ParagraphFormat.ListLevelNumber = 0;
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