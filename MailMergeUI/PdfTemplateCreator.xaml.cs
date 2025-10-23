using Azure;
using Ghostscript.NET;
using Ghostscript.NET.Rasterizer;
using iText.Forms.Form;
using iText.Forms.Form.Element;
using iText.IO.Image;
using iText.Kernel.Colors;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;
using iText.Kernel.Pdf.Xobject;
using iText.Layout;
using iText.Layout.Borders;
using iText.Layout.Element;
using iText.Layout.Properties;
using MailMerge.Data.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using Org.BouncyCastle.Asn1.Pkcs;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.AcroForms;
using PdfSharp.Pdf.Annotations;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Border = System.Windows.Controls.Border;
using Canvas = System.Windows.Controls.Canvas;
using Point = System.Windows.Point;

namespace MailMergeUI
{
    /// <summary>
    /// Interaction logic for PdfTemplateCreator.xaml
    /// </summary>
    public partial class PdfTemplateCreator : UserControl
    {
        private UIElement? _selectedElement;
        private Point _startPoint;
        private bool _isDragging;
        // 👇 NEW STATE MANAGEMENT VARIABLES
        private string? _loadedPdfPath;
        private int _pageCount;
        private int _currentPage;
        // This dictionary is key: it stores the UI elements for each page number.
        private readonly Dictionary<int, List<UIElement>> _pageElements = new Dictionary<int, List<UIElement>>();

        // Constants for PDF page dimensions and coordinate conversion
        private const float WpfToPdfScale = 72f / 96f;
        private const double FontSizeScale = 1.15;
        private readonly MailMergeEngine.MailMergeEngine _mailMergeEngine;

        public PdfTemplateCreator()
        {

            InitializeComponent();

            // Populate the font selection ComboBox
            FontComboBox.ItemsSource = new[] { "Arial", "Calibri", "Times New Roman", "Verdana", "Courier New" };
            FontComboBox.SelectedIndex = 0;

            // Populate the font size ComboBox
            FontSizeComboBox.ItemsSource = new[] { 8, 9, 10, 11, 12, 14, 16, 18, 20, 22, 24, 28, 36, 48, 72 };
            FontSizeComboBox.SelectedItem = 12;

            // Populate the FormFieldComboBox
            FormFieldComboBox.ItemsSource = new[] { "Select Field...", "Radar ID", "Apn", "Type", "Address", "City", "ZIP", "Owner", "Owner Type", "Owner Occ?", "Primary Name", "Primary First", "Mail Address", "Mail City", "Mail State", "Mail ZIP", "Foreclosure", "FCL Stage", "FCL Doc Type", "FCL Rec Date", "Trustee", "Trustee Phone", "TS Number" };
            FormFieldComboBox.SelectedIndex = 0;

            DateFieldComboBox.ItemsSource = new[] { "Select Field...", "Current Day", "Current Month", "Current Year" };
            DateFieldComboBox.SelectedIndex = 0;

            if (!DesignerProperties.GetIsInDesignMode(this))
            {
                _mailMergeEngine = App.Services!.GetRequiredService<MailMergeEngine.MailMergeEngine>();
            }

            // NEW: Initialize for a single, blank page
            _currentPage = 1;
            _pageCount = 1;
            _pageElements[_currentPage] = new List<UIElement>();
            UpdateNavigationUI();
        }

        /// <summary>
        /// Handles the click event for the "Export to PDF" button.
        /// </summary>
        //private void ExportPdf_Click(object sender, RoutedEventArgs e)
        //{
        //    // Ensure the current page's elements are saved before exporting
        //    SaveCurrentPageState();

        //    var saveFileDialog = new SaveFileDialog
        //    {
        //        Filter = "PDF Document (*.pdf)|*.pdf",
        //        Title = "Save PDF Template",
        //        FileName = "Template.pdf"
        //    };

        //    if (saveFileDialog.ShowDialog() != true)
        //        return;

        //    string filename = saveFileDialog.FileName;
        //    float pdfPageWidth = PageSize.A4.GetWidth();
        //    float pdfPageHeight = PageSize.A4.GetHeight();
        //    const float WpfToPdfScale = 72f / 96f;

        //    using (var writer = new PdfWriter(filename))
        //    using (var pdf = new iText.Kernel.Pdf.PdfDocument(writer))
        //    using (var doc = new Document(pdf, new PageSize(pdfPageWidth, pdfPageHeight)))
        //    {
        //        doc.SetMargins(0, 0, 0, 0);

        //        // Loop through each page in the dictionary
        //        foreach (var page in _pageElements)
        //        {
        //            int pageNumber = page.Key;
        //            var elementsOnPage = page.Value;

        //            // If it's not the first page, add a new page to the document
        //            if (pageNumber > 1)
        //            {
        //                doc.Add(new AreaBreak(AreaBreakType.NEXT_PAGE));
        //            }

        //            // Re-render the PDF background for the current page
        //            if (!string.IsNullOrEmpty(_loadedPdfPath))
        //            {
        //                try
        //                {
        //                    using (var pdfiumDoc = PdfiumViewer.PdfDocument.Load(_loadedPdfPath))
        //                    {
        //                        var drawingImage = pdfiumDoc.Render(pageNumber - 1, 96, 96, false);
        //                        using (var ms = new MemoryStream())
        //                        {
        //                            drawingImage.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        //                            var imageData = ms.ToArray();
        //                            var itextBgImage = new iText.Layout.Element.Image(ImageDataFactory.Create(imageData));
        //                            itextBgImage.SetWidth(pdfPageWidth).SetHeight(pdfPageHeight);
        //                            itextBgImage.SetFixedPosition(pageNumber, 0, 0);
        //                            doc.Add(itextBgImage);
        //                        }
        //                    }
        //                }
        //                catch (Exception ex)
        //                {
        //                    System.Diagnostics.Debug.WriteLine($"Error rendering PDF background for page {pageNumber}: {ex.Message}");
        //                }
        //            }

        //            // Loop through all UI elements for this specific page
        //            foreach (UIElement element in elementsOnPage)
        //            {
        //                if (element is Border border)
        //                {
        //                    double leftD = Canvas.GetLeft(border);
        //                    double topD = Canvas.GetTop(border);

        //                    if (border.Child is TextBox textBox)
        //                    {
        //                        float x = (float)(leftD * WpfToPdfScale);
        //                        float w = (float)(textBox.ActualWidth * WpfToPdfScale);
        //                        float h = (float)(textBox.ActualHeight * WpfToPdfScale);
        //                        float y = (float)((TemplateCanvas.Height - topD - textBox.ActualHeight) * WpfToPdfScale);

        //                        if ((textBox.Tag as string)?.Equals("FormField", StringComparison.OrdinalIgnoreCase) == true)
        //                        {
        //                            string fieldName = textBox.Text?.Trim() ?? "field";
        //                            if (fieldName.StartsWith("{") && fieldName.EndsWith("}"))
        //                                fieldName = fieldName.Substring(1, fieldName.Length - 2);

        //                            var ta = new iText.Forms.Form.Element.TextArea(fieldName);
        //                            ta.SetInteractive(true);
        //                            ta.SetValue(textBox.Text ?? string.Empty);
        //                            ta.SetWidth(w);
        //                            ta.SetHeight(h);
        //                            ta.SetFixedPosition(pageNumber, x, y, w);
        //                            ta.SetProperty(iText.Layout.Properties.Property.FONT_SIZE, iText.Layout.Properties.UnitValue.CreatePointValue((float)textBox.FontSize * WpfToPdfScale));
        //                            doc.Add(ta);
        //                        }
        //                        else
        //                        {
        //                            var para = new iText.Layout.Element.Paragraph(textBox.Text ?? string.Empty)
        //                                .SetFontSize((float)textBox.FontSize * WpfToPdfScale)
        //                                .SetFixedPosition(pageNumber, x, y + (float)(h - textBox.FontSize * 1.15 * WpfToPdfScale), w);

        //                            if (textBox.Foreground is SolidColorBrush fgBrush)
        //                            {
        //                                var c = fgBrush.Color;
        //                                para.SetFontColor(new iText.Kernel.Colors.DeviceRgb(c.R, c.G, c.B));
        //                            }
        //                            doc.Add(para);
        //                        }
        //                    }
        //                    else if (border.Child is System.Windows.Controls.Image image)
        //                    {
        //                        if (image.Tag is byte[] imageData)
        //                        {
        //                            float x = (float)(leftD * WpfToPdfScale);
        //                            float w = (float)(image.ActualWidth * WpfToPdfScale);
        //                            float h = (float)(image.ActualHeight * WpfToPdfScale);
        //                            float y = (float)((TemplateCanvas.Height - topD - image.ActualHeight) * WpfToPdfScale);

        //                            var itextImage = new iText.Layout.Element.Image(ImageDataFactory.Create(imageData));
        //                            itextImage.SetWidth(w);
        //                            itextImage.SetHeight(h);
        //                            itextImage.SetFixedPosition(pageNumber, x, y);
        //                            doc.Add(itextImage);
        //                        }
        //                    }
        //                }
        //            }
        //        }

        //        doc.Close();
        //    }

        //    Process.Start(new ProcessStartInfo(filename) { UseShellExecute = true });
        //}

        private void ExportPdf_Click(object sender, RoutedEventArgs e)
        {
            // Ensure the current page's elements are saved before exporting
            SaveCurrentPageState();

            var saveDialog = new SaveFileDialog
            {
                Filter = "PDF Document (*.pdf)|*.pdf",
                Title = "Save PDF Template",
                FileName = "Template.pdf"
            };

            if (saveDialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                ExportToPdf(saveDialog.FileName);
                // Open the generated PDF file using the default system viewer
                Process.Start(new ProcessStartInfo(saveDialog.FileName) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                // Provide a user-friendly error message
                MessageBox.Show($"An error occurred while exporting the PDF: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Debug.WriteLine(ex.ToString());
            }
        }

        /// <summary>
        /// Handles the click event for the "Add Text" button.
        /// </summary>
        private void AddTextBlock_Click(object sender, RoutedEventArgs e)
        {
            var newText = new TextBox
            {
                Text = "New Editable Text",
                Tag = "TextBlock", // Identify this as a simple text block
                FontFamily = new FontFamily(FontComboBox.SelectedItem.ToString()),
                FontSize = (int)FontSizeComboBox.SelectedItem,
                Foreground = (Brush)FindResource("TextDark"), // Set default text color
                Padding = new Thickness(2),
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                MinWidth = 20,
                IsReadOnly = true
            };

            CreateAndAddDraggableItem(newText, 20, 20);
        }

        

        private void CreateAndAddDraggableItem(UIElement content, double x, double y)
        {
            var container = new Border
            {
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(2), // Padding to make the border visible around the content
                Child = content
            };

            // Attach mouse events for selection and dragging
            container.PreviewMouseLeftButtonDown += DraggableItem_PreviewMouseLeftButtonDown;

            if (content is TextBox textBox)
            {
                // 👇 ADD THIS LINE to handle the double-click on the TextBox
                textBox.PreviewMouseDoubleClick += TextBox_PreviewMouseDoubleClick;

                // This line is from the original solution and is still correct
                textBox.LostFocus += TextBox_LostFocus;
            }

            // Set initial position on the canvas
            Canvas.SetLeft(container, x);
            Canvas.SetTop(container, y);

            TemplateCanvas.Children.Add(container);

            // Select the newly created item
            SelectItem(container);
        }

        /// <summary>
        /// Handles the click event for the "Delete" button.
        /// </summary>
        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedElement != null)
            {
                TemplateCanvas.Children.Remove(_selectedElement);
                SelectItem(null); // Deselect
            }
        }

        /// <summary>
        /// Handles font selection changes from the ComboBox.
        /// </summary>
        private void FontComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_selectedElement != null && FontComboBox.SelectedItem is string selectedFont)
            {
                // The content is inside a Border, so we access it via the Child property.
                if (((Border)_selectedElement).Child is TextBox content)
                {
                    content.FontFamily = new FontFamily(selectedFont);
                }
            }
        }

        /// <summary>
        /// Initiates the dragging process when an item on the canvas is clicked.
        /// </summary>
        private void DraggableItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is UIElement draggableItem)
            {
                SelectItem(draggableItem);
                _startPoint = e.GetPosition(TemplateCanvas);
                _isDragging = true;
                draggableItem.CaptureMouse();
                e.Handled = true; // Prevent event from bubbling further
            }
        }

        /// <summary>
        /// Handles mouse movement across the canvas to move the selected item.
        /// </summary>
        private void TemplateCanvas_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging && _selectedElement != null)
            {
                Point currentPosition = e.GetPosition(TemplateCanvas);
                double offsetX = currentPosition.X - _startPoint.X;
                double offsetY = currentPosition.Y - _startPoint.Y;

                double newX = Canvas.GetLeft(_selectedElement) + offsetX;
                double newY = Canvas.GetTop(_selectedElement) + offsetY;

                // Basic boundary check to keep elements inside the canvas
                if (newX < 0) newX = 0;
                if (newY < 0) newY = 0;
                if (newX + ((FrameworkElement)_selectedElement).ActualWidth > TemplateCanvas.ActualWidth)
                {
                    newX = TemplateCanvas.ActualWidth - ((FrameworkElement)_selectedElement).ActualWidth;
                }
                if (newY + ((FrameworkElement)_selectedElement).ActualHeight > TemplateCanvas.ActualHeight)
                {
                    newY = TemplateCanvas.ActualHeight - ((FrameworkElement)_selectedElement).ActualHeight;
                }

                Canvas.SetLeft(_selectedElement, newX);
                Canvas.SetTop(_selectedElement, newY);

                _startPoint = currentPosition;
            }
        }

        /// <summary>
        /// Ends the dragging process when the mouse button is released.
        /// </summary>
        private void TemplateCanvas_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                _selectedElement?.ReleaseMouseCapture();
            }
        }

        /// <summary>
        /// Handles the selection of an item, updating its visual state.
        /// </summary>
        private void SelectItem(UIElement? itemToSelect)
        {
            // Deselect the previously selected item
            if (_selectedElement is Border oldBorder)
            {
                oldBorder.BorderBrush = Brushes.Transparent;
                oldBorder.Background = Brushes.Transparent;
            }

            _selectedElement = itemToSelect;

            // Select the new item
            if (_selectedElement is Border newBorder)
            {
                newBorder.BorderBrush = (Brush)FindResource("PrimaryBlue");
                newBorder.Background = (Brush)FindResource("PrimaryBlueLight");

                // Update toolbar controls to reflect the selected item's properties
                if (newBorder.Child is TextBox content)
                {
                    FontComboBox.SelectedItem = content.FontFamily.Source;
                    FontSizeComboBox.SelectedItem = (int)content.FontSize;
                }
            }

            // Enable/disable delete button based on selection
            DeleteButton.IsEnabled = (_selectedElement != null);
            FontComboBox.IsEnabled = (_selectedElement != null);
            FontSizeComboBox.IsEnabled = (_selectedElement != null);
        }

        /// <summary>
        /// Deselects any item when the canvas background is clicked.
        /// </summary>
        private void TemplateCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // If the click was directly on the canvas and not an item, deselect.
            if (e.Source == TemplateCanvas)
            {
                SelectItem(null);
            }
        }

        private void FontSizeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_selectedElement != null && FontSizeComboBox.SelectedItem is int selectedSize)
            {
                if (((Border)_selectedElement).Child is TextBox content)
                {
                    content.FontSize = selectedSize;
                }
            }
        }

        private void AddImage_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.gif",
                Title = "Select an Image"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    // Load image for display
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(openFileDialog.FileName);
                    bitmap.EndInit();

                    // Read image data into byte array for storage
                    byte[] imageData = File.ReadAllBytes(openFileDialog.FileName);

                    var newImage = new System.Windows.Controls.Image
                    {
                        Source = bitmap,
                        Tag = imageData, // Store byte array for PDF export
                        Width = 150
                    };

                    CreateAndAddDraggableItem(newImage, 20, 100);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading image: {ex.Message}");
                }
            }
        }

        private void TextBox_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                textBox.IsReadOnly = false;
                textBox.Focus();
                textBox.SelectAll();

                // This is VERY IMPORTANT. It stops the click event from bubbling up to the
                // parent Border, which would incorrectly start a drag operation.
                e.Handled = true;
            }
        }

        private void FormFieldComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FormFieldComboBox.SelectedItem is string selectedField && selectedField != "Select Field...")
            {
                var newField = new TextBox
                {
                    Text = "{" + selectedField + "}", // Use the selected field name
                    Tag = "FormField",
                    FontFamily = new FontFamily(FontComboBox.SelectedItem.ToString()),
                    FontSize = (int)FontSizeComboBox.SelectedItem,
                    Foreground = (Brush)FindResource("TextDark"),
                    Padding = new Thickness(2),
                    BorderBrush = (Brush)FindResource("DividerColor"),
                    BorderThickness = new Thickness(1),
                    Background = Brushes.White,
                    MinWidth = 20,
                    IsReadOnly = true
                };

                CreateAndAddDraggableItem(newField, 20, 60);

                // Reset the ComboBox to the default "Select Field..." option
                FormFieldComboBox.SelectedIndex = 0;
            }
        }

        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                textBox.IsReadOnly = true;
            }
        }

        private void LoadPdf_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "PDF Documents (*.pdf)|*.pdf",
                Title = "Load PDF Template"
            };

            if (openFileDialog.ShowDialog() != true) return;

            try
            {
                _loadedPdfPath = openFileDialog.FileName;

                // Use iText to quickly get page count
                using (var pdfDoc = new iText.Kernel.Pdf.PdfDocument(new PdfReader(_loadedPdfPath)))
                {
                    _pageCount = pdfDoc.GetNumberOfPages();
                }

                // Reset state
                _pageElements.Clear();
                TemplateCanvas.Children.Clear(); // Clear any existing elements
                for (int i = 1; i <= _pageCount; i++)
                {
                    _pageElements[i] = new List<UIElement>(); // Initialize list for each page
                }

                LoadPage(1); // Load the first page

                // Enable UI controls
                ExportButton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading PDF: {ex.Message}");
                _loadedPdfPath = null;
            }
        }

        private void PrevPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage > 1)
            {
                LoadPage(_currentPage - 1);
            }
        }

        private void NextPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage < _pageCount)
            {
                LoadPage(_currentPage + 1);
            }
        }

        private void LoadPage(int pageNumber)
        {
            if (_loadedPdfPath == null || pageNumber < 1 || pageNumber > _pageCount)
                return;

            // 1. Save elements on the current page before switching
            if (_currentPage > 0)
            {
                SaveCurrentPageState();
            }

            // Clear canvas for the new page
            TemplateCanvas.Children.Clear();

            // 2. Use Ghostscript to render the page
            using (var rasterizer = new GhostscriptRasterizer())
            {
                // Optional: specify Ghostscript version if needed
                var version = GhostscriptVersionInfo.GetLastInstalledVersion(
                    GhostscriptLicense.GPL | GhostscriptLicense.AFPL,
                    GhostscriptLicense.GPL
                );

                rasterizer.Open(_loadedPdfPath, version, false);

                // ✅ Increase DPI for higher quality
                int dpi = 600; // Try 300 or 600 for ultra sharpness
                using (var img = rasterizer.GetPage(dpi, pageNumber))
                {
                    // Convert System.Drawing.Image → BitmapImage (for WPF)
                    using (var ms = new MemoryStream())
                    {
                        img.Save(ms, ImageFormat.Png);
                        ms.Position = 0;

                        var bitmapImage = new BitmapImage();
                        bitmapImage.BeginInit();
                        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                        bitmapImage.StreamSource = ms;
                        bitmapImage.EndInit();

                        PdfBackgroundBrush.ImageSource = bitmapImage;
                        bitmapImage.Freeze(); // optional for perf

                        TemplateCanvas.Width = bitmapImage.PixelWidth;
                        TemplateCanvas.Height = bitmapImage.PixelHeight;
                    }
                }
            }

            _currentPage = pageNumber;

            // 3. Restore page state and update UI
            RestorePageState(_currentPage);
            UpdateNavigationUI();
        }

        private void SaveCurrentPageState()
        {
            if (_pageElements.ContainsKey(_currentPage))
            {
                _pageElements[_currentPage] = TemplateCanvas.Children.Cast<UIElement>().ToList();
            }
        }

        private void RestorePageState(int pageNumber)
        {
            TemplateCanvas.Children.Clear();
            if (_pageElements.ContainsKey(pageNumber))
            {
                foreach (var element in _pageElements[pageNumber])
                {
                    TemplateCanvas.Children.Add(element);
                }
            }
        }

        private void UpdateNavigationUI()
        {
            PageNumberText.Text = $"Page {_currentPage} of {_pageCount}";
            PrevPage.IsEnabled = _currentPage > 1;
            NextPage.IsEnabled = _currentPage < _pageCount;
        }

        private async void LoadCSV_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog() { Filter = "CSV Files|*.csv" };
            string csvPath = "";
            List<PropertyRecord> records = new();
            try
            {
                if (ofd.ShowDialog() == true)
                {
                    
                    csvPath = ofd.FileName;

                }

                SaveCurrentPageState();

                //Export the template to temp pdf file
                string tempTemplateFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"temp_template_preview_{Guid.NewGuid()}.pdf");
                ExportToPdf(tempTemplateFile);

                //Load csv
                records = _mailMergeEngine.ReadCsv(csvPath);
                

                //create and show preview
                string tempPreviewFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"temp_CSV_preview_{Guid.NewGuid()}.pdf");
                _mailMergeEngine.ExportBatch(tempTemplateFile, records, tempPreviewFile);

                CanvasBorder.Visibility = Visibility.Collapsed;
                // this asynchronously ensures the CoreWebView2 is ready
                await PdfWebView.EnsureCoreWebView2Async();
                // navigate to the temp file
                PdfWebView.CoreWebView2.Navigate(new Uri(tempPreviewFile).AbsoluteUri);




            }
            catch (Exception ex)
            {
                Console.WriteLine($"{ex.Message}");
            }
        }

        private void ExportToPdf(string filename)
        {
            // Define PDF page dimensions
            var pdfPageWidth = PageSize.A4.GetWidth();
            var pdfPageHeight = PageSize.A4.GetHeight();
            var pageSize = new PageSize(pdfPageWidth, pdfPageHeight);

            using (var writer = new PdfWriter(filename))
            using (var pdf = new iText.Kernel.Pdf.PdfDocument(writer))
            using (var doc = new Document(pdf, pageSize))
            {
                doc.SetMargins(0, 0, 0, 0);

                // Iterate through each page's elements
                foreach (var pageEntry in _pageElements.OrderBy(p => p.Key))
                {
                    int pageNumber = pageEntry.Key;
                    var elementsOnPage = pageEntry.Value;

                    // Add a new page for all pages after the first
                    if (pageNumber > 1)
                    {
                        doc.Add(new AreaBreak(AreaBreakType.NEXT_PAGE));
                    }

                    // Render the background image if a source PDF was loaded
                    RenderPageBackground(doc, _loadedPdfPath, pageNumber, pdfPageWidth, pdfPageHeight);

                    // Add each UI element to the current page
                    foreach (UIElement element in elementsOnPage)
                    {
                        AddUIElementToPdf(doc, element, pageNumber);
                    }
                }
            }
        }

        private void RenderPageBackground(Document doc, string pdfPath, int pageNumber, float pageWidth, float pageHeight)
        {
            if (string.IsNullOrEmpty(pdfPath)) return;

            try
            {
                using (var pdfiumDoc = PdfiumViewer.PdfDocument.Load(pdfPath))
                {
                    // Render the page as a bitmap image at 96 DPI
                    var drawingImage = pdfiumDoc.Render(pageNumber - 1, 96, 96, false);
                    using (var ms = new MemoryStream())
                    {
                        drawingImage.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                        var imageData = ms.ToArray();
                        var itextBgImage = new iText.Layout.Element.Image(ImageDataFactory.Create(imageData));
                        itextBgImage.SetWidth(pageWidth).SetHeight(pageHeight);
                        // Fixed position is relative to the current page
                        itextBgImage.SetFixedPosition(pageNumber, 0, 0);
                        doc.Add(itextBgImage);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error rendering PDF background for page {pageNumber}: {ex.Message}");
                // Log the exception but allow the rest of the export to continue
            }
        }

        private void AddUIElementToPdf(Document doc, UIElement element, int pageNumber)
        {
            if (element is Border border)
            {
                var child = border.Child;
                if (child is TextBox textBox)
                {
                    HandleTextBox(doc, textBox, pageNumber, border);
                }
                else if (child is System.Windows.Controls.Image image)
                {
                    HandleImage(doc, image, pageNumber, border);
                }
                // Add other element types here as needed
            }
        }

        private void HandleTextBox(Document doc, TextBox textBox, int pageNumber, Border border)
        {
            // Get scaled coordinates
            var (x, y, w, h) = ConvertWpfToPdfPosition(
                Canvas.GetLeft(border),
                Canvas.GetTop(border),
                textBox.ActualWidth,
                textBox.ActualHeight,
                TemplateCanvas.Height,
                WpfToPdfScale);

            if ((textBox.Tag as string)?.Equals("FormField", StringComparison.OrdinalIgnoreCase) == true)
            {
                string fieldName = GetFormFieldName(textBox.Text);
                var ta = new iText.Forms.Form.Element.TextArea(fieldName);
                ta.SetInteractive(true);
                ta.SetValue(textBox.Text ?? string.Empty);
                ta.SetWidth(w).SetHeight(h);
                ta.SetFixedPosition(pageNumber, x, y, w);
                ta.SetProperty(Property.FONT_SIZE, UnitValue.CreatePointValue((float)textBox.FontSize * WpfToPdfScale));
                doc.Add(ta);
            }
            else
            {
                var para = new Paragraph(textBox.Text ?? string.Empty)
                    .SetFontSize((float)textBox.FontSize * WpfToPdfScale);
                // Adjust the y-position to account for font size difference
                para.SetFixedPosition(pageNumber, x, y, w);

                if (textBox.Foreground is SolidColorBrush fgBrush)
                {
                    var c = fgBrush.Color;
                    para.SetFontColor(new DeviceRgb(c.R, c.G, c.B));
                }
                doc.Add(para);
            }
        }

        private void HandleImage(Document doc, System.Windows.Controls.Image image, int pageNumber, Border border)
        {
            if (image.Tag is byte[] imageData)
            {
                var (x, y, w, h) = ConvertWpfToPdfPosition(
                    Canvas.GetLeft(border),
                    Canvas.GetTop(border),
                    image.ActualWidth,
                    image.ActualHeight,
                    TemplateCanvas.Height,
                    WpfToPdfScale);

                var itextImage = new iText.Layout.Element.Image(ImageDataFactory.Create(imageData));
                itextImage.SetWidth(w).SetHeight(h);
                itextImage.SetFixedPosition(pageNumber, x, y);
                doc.Add(itextImage);
            }
        }

        private (float x, float y, float w, float h) ConvertWpfToPdfPosition(
        double wpfLeft, double wpfTop, double wpfWidth, double wpfHeight, double canvasHeight, float scaleFactor)
        {
            float x = (float)(wpfLeft * scaleFactor);
            float w = (float)(wpfWidth * scaleFactor);
            float h = (float)(wpfHeight * scaleFactor);
            // PDF coordinates start from the bottom-left, WPF from top-left.
            // Calculate the y-coordinate of the top edge in the PDF coordinate system
            float yTop = (float)((canvasHeight - wpfTop) * scaleFactor);
            // Calculate the y-coordinate of the bottom edge in the PDF coordinate system
            float y = yTop - h; // h is the scaled height
            return (x, y, w, h);
        }

        private string GetFormFieldName(string text)
        {
            string fieldName = text?.Trim() ?? "field";
            if (fieldName.StartsWith("{") && fieldName.EndsWith("}"))
            {
                fieldName = fieldName.Substring(1, fieldName.Length - 2);
            }
            return fieldName;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            PdfWebView.Visibility = Visibility.Collapsed;
            CanvasBorder.Visibility = Visibility.Visible;

        }

        private void DateFieldComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DateFieldComboBox.SelectedIndex != 0)
            {
                var newText = new TextBox
                {
                    Tag = "TextBlock", // Identify this as a simple text block
                    FontFamily = new FontFamily(FontComboBox.SelectedItem.ToString()),
                    FontSize = (int)FontSizeComboBox.SelectedItem,
                    Foreground = (Brush)FindResource("TextDark"), // Set default text color
                    Padding = new Thickness(2),
                    BorderThickness = new Thickness(0),
                    Background = Brushes.Transparent,
                    AcceptsReturn = true,
                    TextWrapping = TextWrapping.Wrap,
                    MinWidth = 20,
                    IsReadOnly = true
                };

                if (DateFieldComboBox.SelectedIndex == 1)
                {
                    newText.Text = DateTime.Now.Day.ToString();
                }
                else if (DateFieldComboBox.SelectedIndex == 2)
                {
                    newText.Text = DateTime.Now.ToString("MMM", CultureInfo.InvariantCulture);
                }
                else if (DateFieldComboBox.SelectedIndex == 3)
                {
                    newText.Text = DateTime.Now.Year.ToString();
                }

                CreateAndAddDraggableItem(newText, 20, 20);
            }
        }
    }
}
