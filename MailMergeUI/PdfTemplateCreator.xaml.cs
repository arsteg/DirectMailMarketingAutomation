using Azure;
using iText.Forms.Form;
using iText.Forms.Form.Element;
using iText.IO.Image;
using iText.Kernel.Colors;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Xobject;
using iText.Layout;
using iText.Layout.Borders;
using iText.Layout.Element;
using iText.Layout.Properties;
using Microsoft.Win32;
using Org.BouncyCastle.Asn1.Pkcs;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.AcroForms;
using PdfSharp.Pdf.Annotations;
using System;
using System.Diagnostics;
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

        public PdfTemplateCreator()
        {
            InitializeComponent();
            // Populate the font selection ComboBox
            FontComboBox.ItemsSource = new[] { "Arial", "Calibri", "Times New Roman", "Verdana", "Courier New" };
            FontComboBox.SelectedIndex = 0;

            // Populate the font size ComboBox
            FontSizeComboBox.ItemsSource = new[] { 8, 9, 10, 11, 12, 14, 16, 18, 20, 22, 24, 28, 36, 48, 72 };
            FontSizeComboBox.SelectedItem = 12;

            // 👇 NEW: Populate the FormFieldComboBox
            FormFieldComboBox.ItemsSource = new[] { "Select Field...", "Radar ID", "Apn", "Type", "Address", "City", "ZIP", "Owner", "Owner Type", "Owner Occ?", "Primary Name", "Primary First", "Mail Address", "Mail City", "Mail State", "Mail ZIP", "Foreclosure", "FCL Stage", "FCL Doc Type", "FCL Rec Date", "Trustee", "Trustee Phone", "TS Number" };
            FormFieldComboBox.SelectedIndex = 0;
        }

        /// <summary>
        /// Handles the click event for the "Export to PDF" button.
        /// </summary>
        private void ExportPdf_Click(object sender, RoutedEventArgs e)
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "PDF Document (*.pdf)|*.pdf",
                Title = "Save PDF Template",
                FileName = "Template.pdf"
            };

            if (saveFileDialog.ShowDialog() != true)
                return;

            string filename = saveFileDialog.FileName;
            float pageWidth = (float)TemplateCanvas.Width;
            float pageHeight = (float)TemplateCanvas.Height;

            using (var writer = new PdfWriter(filename))
            using (var pdf = new iText.Kernel.Pdf.PdfDocument(writer))
            using (var doc = new Document(pdf, new PageSize(pageWidth, pageHeight)))
            {
                doc.SetMargins(0, 0, 0, 0);
                int pageNumber = 1;

                foreach (UIElement element in TemplateCanvas.Children)
                {
                    if(element is Border border)
                    {
                        double leftD = Canvas.GetLeft(border);
                        double topD = Canvas.GetTop(border);

                        if (border.Child is TextBox textBox)
                        {
                            if (double.IsNaN(leftD)) leftD = 0;
                            if (double.IsNaN(topD)) topD = 0;

                            double widthD = textBox.ActualWidth;
                            double heightD = textBox.ActualHeight;

                            float x = (float)leftD;
                            float y = (float)(pageHeight - topD - heightD); // convert top-left -> bottom-left
                            float w = (float)widthD;
                            float h = (float)heightD;

                            if ((textBox.Tag as string)?.Equals("FormField", StringComparison.OrdinalIgnoreCase) == true)
                            {
                                string fieldName = textBox.Text?.Trim() ?? "field";
                                if (fieldName.StartsWith("{") && fieldName.EndsWith("}"))
                                    fieldName = fieldName.Substring(1, fieldName.Length - 2);

                                var ta = new iText.Forms.Form.Element.TextArea(fieldName);

                                // Make interactive so layout creates an AcroForm field
                                ta.SetInteractive(true);

                                // Set the field's default value
                                ta.SetValue(textBox.Text ?? string.Empty);

                                // IMPORTANT: set width/height using helpers (avoid passing raw floats into SetProperty)
                                ta.SetWidth(w);     // sets width in points
                                ta.SetHeight(h);    // sets height in points   <-- fixes the cast error

                                // Fix position (x,y,width). Note SetFixedPosition(x,y,width) is OK; height is set by SetHeight.
                                ta.SetFixedPosition(pageNumber, x, y, w);

                                // Font size: use UnitValue to set FONT_SIZE property
                                ta.SetProperty(iText.Layout.Properties.Property.FONT_SIZE, iText.Layout.Properties.UnitValue.CreatePointValue((float)textBox.FontSize));

                                // Border / appearance using layout helpers
                                //ta.SetProperty(iText.Layout.Properties.Property.BORDER, new iText.Layout.Borders.SolidBorder(1f));

                                doc.Add(ta);
                            }
                            else
                            {
                                var para = new iText.Layout.Element.Paragraph(textBox.Text ?? string.Empty)
                                    .SetFontSize((float)textBox.FontSize)
                                    .SetFixedPosition(pageNumber, x, y + (float)(h - textBox.FontSize * 1.15), w);

                                if (textBox.Foreground is SolidColorBrush fgBrush)
                                {
                                    var c = fgBrush.Color;
                                    para.SetFontColor(new iText.Kernel.Colors.DeviceRgb(c.R, c.G, c.B));
                                }

                                doc.Add(para);
                            }
                        }
                        else if (border.Child is System.Windows.Controls.Image image)
                        {
                            if (image.Tag is byte[] imageData)
                            {
                                // Get position and size
                                
                                if (double.IsNaN(leftD)) leftD = 0;
                                if (double.IsNaN(topD)) topD = 0;

                                double widthD = image.ActualWidth;
                                double heightD = image.ActualHeight;

                                // Convert WPF's top-left coordinates to iText's bottom-left coordinates
                                float x = (float)leftD;
                                float y = (float)(pageHeight - topD - heightD);
                                float w = (float)widthD;
                                float h = (float)heightD;

                                // Create an iText Image object from the byte array stored in the Tag
                                var itextImage = new iText.Layout.Element.Image(ImageDataFactory.Create(imageData));

                                // Set the size and absolute position of the image on the page
                                itextImage.SetWidth(w);
                                itextImage.SetHeight(h);
                                itextImage.SetFixedPosition(1, x, y); // Using page number 1 for now

                                // Add the image to the document
                                doc.Add(itextImage);
                            }
                        }
                    }

                   
                    
                }

                doc.Close();
            }

            Process.Start(new ProcessStartInfo(filename) { UseShellExecute = true });
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
                MinWidth = 100,
                IsReadOnly = true
            };

            CreateAndAddDraggableItem(newText, 20, 20);
        }

        /// <summary>
        /// Handles the click event for the "Add Form Field" button.
        /// </summary>
        private void AddFormField_Click(object sender, RoutedEventArgs e)
        {
            var newField = new TextBox
            {
                Text = "{FormFieldName}",
                Tag = "FormField", // Identify this as an interactive form field
                FontFamily = new FontFamily(FontComboBox.SelectedItem.ToString()),
                FontSize = (int)FontSizeComboBox.SelectedItem,
                Foreground = (Brush)FindResource("TextDark"), // Set default text color
                Padding = new Thickness(2),
                BorderBrush = (Brush)FindResource("DividerColor"),
                BorderThickness = new Thickness(1),
                Background = Brushes.White,
                MinWidth = 150,
                IsReadOnly = true
            };

            CreateAndAddDraggableItem(newField, 20, 60);
        }
        // ... rest of the existing code ...
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
                    MinWidth = 150,
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
    }
}
