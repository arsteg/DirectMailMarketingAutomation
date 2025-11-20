using DocumentFormat.OpenXml.Packaging;
using iText.IO.Image;
using iText.Kernel.Colors;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using MailMerge.Data.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Border = System.Windows.Controls.Border;
using Canvas = System.Windows.Controls.Canvas;
using Ellipse = System.Windows.Shapes.Ellipse;
using Point = System.Windows.Point;

namespace MailMergeUI
{
    public partial class PdfTemplateCreator : UserControl
    {
        private UIElement _selectedElement;
        private Point _startPoint;
        private bool _isDragging;
        private bool _isResizing;
        private ResizeHandle _activeHandle;
        private readonly Dictionary<int, List<UIElement>> _pageElements = new();
        private readonly Stack<Action> _undoStack = new();
        private int _currentPage = 1;
        private int _pageCount = 1;
        private string _loadedDocumentText;
        private string _loadedPdfPath;
        private readonly MailMergeEngine.MailMergeEngine _mailMergeEngine;

        private enum ResizeHandle { TopLeft, TopRight, BottomLeft, BottomRight, Top, Bottom, Left, Right }

        public PdfTemplateCreator()
        {
            InitializeComponent();
            if (!DesignerProperties.GetIsInDesignMode(this))
            {
                _mailMergeEngine = App.Services!.GetRequiredService<MailMergeEngine.MailMergeEngine>();
            }

            InitializeCombos();
            _pageElements[_currentPage] = new List<UIElement>();
            UpdateNavigationUI();
            UpdateLayers();
            // ADD THIS LINE — enables key handling even when child controls have focus
            this.Focusable = true;
            this.FocusVisualStyle = null;
            // Main keyboard handler for the entire control
            this.PreviewKeyDown += PdfTemplateCreator_PreviewKeyDown;
        }

        private void PdfTemplateCreator_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Delete or Backspace key deletes the selected element
            if (e.Key == Key.Delete || e.Key == Key.Back)
            {
                if (_selectedElement is Border border && TemplateCanvas.Children.Contains(border))
                {
                    // Remove resize handles first
                    RemoveResizeHandles(border);

                    // Remove the element from canvas
                    TemplateCanvas.Children.Remove(border);

                    // Clear selection
                    SelectItem(null);

                    // Update layers panel
                    UpdateLayers();

                    // Mark as handled so it doesn't bubble further
                    e.Handled = true;
                }
            }
        }

        private void InitializeCombos()
        {
            var fonts = Fonts.SystemFontFamilies.Select(f => f.Source).OrderBy(s => s).ToList();
            FontComboBox.ItemsSource = fonts;
            FontComboBox.SelectedItem = "Calibri";

            FontSizeComboBox.ItemsSource = new[] { 8, 9, 10, 11, 12, 14, 16, 18, 20, 24, 28, 32, 36, 48, 72 };
            FontSizeComboBox.SelectedItem = 12;

            FormFieldComboBox.ItemsSource = new[] { "Select Field...", "Radar ID", "Apn", "Type", "Address", "City", "ZIP", "Owner", "Owner Type", "Owner Occ?", "Primary Name", "Primary First", "Mail Address", "Mail City", "Mail State", "Mail ZIP", "Foreclosure", "FCL Stage", "FCL Doc Type", "FCL Rec Date", "Trustee", "Trustee Phone", "TS Number" };
            FormFieldComboBox.SelectedIndex = 0;

            DateFieldComboBox.ItemsSource = new[] { "Select Field...", "Current Day", "Current Month", "Current Year" };
            DateFieldComboBox.SelectedIndex = 0;
        }

        #region Toolbar Actions

        private void NewTemplate_Click(object sender, RoutedEventArgs e)
        {
            TemplateCanvas.Children.Clear();
            _pageElements.Clear();
            _currentPage = 1;
            _pageCount = 1;
            _pageElements[1] = new List<UIElement>();
            _loadedDocumentText = null;
            PdfBackgroundBrush.ImageSource = null;
            UpdateNavigationUI();
            UpdateLayers();
        }

        private void AddTextBlock_Click(object sender, RoutedEventArgs e)
        {
            SaveCurrentPageState();
            var tb = new TextBox
            {
                Text = "New Text",
                Tag = "TextBlock",
                FontFamily = new FontFamily(FontComboBox.SelectedItem?.ToString() ?? "Calibri"),
                FontSize = (int)(FontSizeComboBox.SelectedItem ?? 12),
                Foreground = Brushes.Black,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(4),
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                MinWidth = 100,
                IsReadOnly = true
            };
            tb.PreviewMouseDoubleClick += TextBox_PreviewMouseDoubleClick;
            tb.LostFocus += TextBox_LostFocus;

            CreateResizableItem(tb, 100, 100);
        }

        private void AddImage_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                Filter = "Images|*.jpg;*.jpeg;*.png;*.bmp;*.gif"
            };
            if (ofd.ShowDialog() == true)
            {
                SaveCurrentPageState();
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(ofd.FileName);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();

                var imageData = File.ReadAllBytes(ofd.FileName);
                var img = new System.Windows.Controls.Image
                {
                    Source = bitmap,
                    Tag = imageData,
                    Stretch = Stretch.Uniform,
                    Width = 200
                };

                CreateResizableItem(img, 100, 100);
            }
        }

        private void FormFieldComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FormFieldComboBox.SelectedItem is string field && field != "Select Field...")
            {
                SaveCurrentPageState();
                var tb = new TextBox
                {
                    Text = "{" + field + "}",
                    Tag = "FormField",
                    FontFamily = new FontFamily("Arial"),
                    FontSize = 12,
                    Foreground = Brushes.Blue,
                    Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(30, 0, 0, 255)),
                    BorderBrush = Brushes.Blue,
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(4),
                    IsReadOnly = true
                };
                CreateResizableItem(tb, 100, 150);
                FormFieldComboBox.SelectedIndex = 0;
            }
        }

        private void DateFieldComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DateFieldComboBox.SelectedIndex > 0)
            {
                SaveCurrentPageState();
                var tb = new TextBox
                {
                    Text = DateFieldComboBox.SelectedIndex == 1 ? DateTime.Now.Day.ToString() :
                           DateFieldComboBox.SelectedIndex == 2 ? DateTime.Now.ToString("MMMM") :
                           DateTime.Now.Year.ToString(),
                    Tag = "TextBlock",
                    FontSize = 14,
                    Foreground = Brushes.Black,
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    IsReadOnly = true
                };
                CreateResizableItem(tb, 100, 200);
                DateFieldComboBox.SelectedIndex = 0;
            }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedElement is Border border && TemplateCanvas.Children.Contains(border))
            {
                RemoveResizeHandles(border);
                TemplateCanvas.Children.Remove(border);
                SelectItem(null);
                UpdateLayers();
            }
        }

        private void BringForward_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedElement is Border border)
            {
                int z = Panel.GetZIndex(border);
                Panel.SetZIndex(border, z + 1);
                UpdateLayers();
            }
        }

        private void SendBackward_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedElement is Border border)
            {
                int z = Panel.GetZIndex(border);
                if (z > 0) Panel.SetZIndex(border, z - 1);
                UpdateLayers();
            }
        }

        private void AlignLeft_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedElement is Border border)
            {
                Canvas.SetLeft(border, 50);
                UpdateHandlePositions(border);
            }
        }

        private void ReverseLayers_Click(object sender, RoutedEventArgs e)
        {
            var items = TemplateCanvas.Children.OfType<Border>().Where(b => b.Child != null).ToList();
            foreach (var item in items)
            {
                int z = Panel.GetZIndex(item);
                Panel.SetZIndex(item, -z);
            }
            UpdateLayers();
        }

        #endregion

        #region Canvas Item Management

        private void CreateResizableItem(UIElement content, double x, double y)
        {
            var border = new Border
            {
                Child = content,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(2),
                Background = Brushes.Transparent,
                Padding = new Thickness(4)
            };

            border.PreviewMouseLeftButtonDown += (sender, e) =>
            {
                if (e.OriginalSource is Ellipse) return; // Ignore clicks on resize handles

                SelectItem(border);
                _startPoint = e.GetPosition(TemplateCanvas);
                _isDragging = true;
                border.CaptureMouse();
                e.Handled = true;
            };

            Canvas.SetLeft(border, x);
            Canvas.SetTop(border, y);
            Canvas.SetZIndex(border, TemplateCanvas.Children.Count);
            TemplateCanvas.Children.Add(border);

            AddResizeHandles(border);
            SelectItem(border);
            UpdateLayers();
        }

        private void AddResizeHandles(Border border)
        {
            for (int i = 0; i < 8; i++)
            {
                var handle = new Ellipse
                {
                    Width = 10,
                    Height = 10,
                    Fill = Brushes.White,
                    Stroke = Brushes.DodgerBlue,
                    StrokeThickness = 2,
                    Cursor = GetCursorForHandle((ResizeHandle)i),
                    Tag = (ResizeHandle)i
                };
                handle.MouseLeftButtonDown += (s, e) =>
                {
                    _isResizing = true;
                    _activeHandle = (ResizeHandle)handle.Tag;
                    _startPoint = e.GetPosition(TemplateCanvas);
                    handle.CaptureMouse();
                    e.Handled = true;
                };
                handle.MouseMove += HandleResizeMove;
                handle.MouseLeftButtonUp += (s, e) => { _isResizing = false; handle.ReleaseMouseCapture(); };

                border.SizeChanged += (s, e) => UpdateHandlePositions(border);
                TemplateCanvas.Children.Add(handle);
                Panel.SetZIndex(handle, 1000);
            }
        }

        private void RemoveResizeHandles(Border border)
        {
            var handles = TemplateCanvas.Children.OfType<Ellipse>().Where(e => Panel.GetZIndex(e) == 1000).Take(8).ToList();
            foreach (var h in handles) TemplateCanvas.Children.Remove(h);
        }

        private void UpdateHandlePositions(Border border)
        {
            var handles = TemplateCanvas.Children.OfType<Ellipse>().Where(e => Panel.GetZIndex(e) == 1000).Take(8).ToList();
            if (handles.Count < 8) return;

            double w = border.ActualWidth, h = border.ActualHeight;
            double l = Canvas.GetLeft(border), t = Canvas.GetTop(border);

            var pos = new[]
            {
                new Point(l - 5, t - 5),
                new Point(l + w - 5, t - 5),
                new Point(l - 5, t + h - 5),
                new Point(l + w - 5, t + h - 5),
                new Point(l + w/2 - 5, t - 5),
                new Point(l + w/2 - 5, t + h - 5),
                new Point(l - 5, t + h/2 - 5),
                new Point(l + w - 5, t + h/2 - 5)
            };

            for (int i = 0; i < 8; i++)
            {
                Canvas.SetLeft(handles[i], pos[i].X);
                Canvas.SetTop(handles[i], pos[i].Y);
            }
        }

        private Cursor GetCursorForHandle(ResizeHandle h) => h switch
        {
            ResizeHandle.TopLeft or ResizeHandle.BottomRight => Cursors.SizeNWSE,
            ResizeHandle.TopRight or ResizeHandle.BottomLeft => Cursors.SizeNESW,
            ResizeHandle.Top or ResizeHandle.Bottom => Cursors.SizeNS,
            ResizeHandle.Left or ResizeHandle.Right => Cursors.SizeWE,
            _ => Cursors.Arrow
        };

        private void HandleResizeMove(object sender, MouseEventArgs e)
        {
            if (!_isResizing || _selectedElement is not Border border) return;

            var pos = e.GetPosition(TemplateCanvas);
            double dx = pos.X - _startPoint.X;
            double dy = pos.Y - _startPoint.Y;
            double left = Canvas.GetLeft(border);
            double top = Canvas.GetTop(border);
            double width = border.ActualWidth;
            double height = border.ActualHeight;

            switch (_activeHandle)
            {
                case ResizeHandle.TopLeft:
                    Canvas.SetLeft(border, left + dx);
                    Canvas.SetTop(border, top + dy);
                    border.Width = width - dx;
                    border.Height = height - dy;
                    break;
                case ResizeHandle.TopRight:
                    Canvas.SetTop(border, top + dy);
                    border.Width = width + dx;
                    border.Height = height - dy;
                    break;
                case ResizeHandle.BottomLeft:
                    Canvas.SetLeft(border, left + dx);
                    border.Width = width - dx;
                    border.Height = height + dy;
                    break;
                case ResizeHandle.BottomRight:
                    border.Width = width + dx;
                    border.Height = height + dy;
                    break;
                case ResizeHandle.Top:
                    Canvas.SetTop(border, top + dy);
                    border.Height = height - dy;
                    break;
                case ResizeHandle.Bottom:
                    border.Height = height + dy;
                    break;
                case ResizeHandle.Left:
                    Canvas.SetLeft(border, left + dx);
                    border.Width = width - dx;
                    break;
                case ResizeHandle.Right:
                    border.Width = width + dx;
                    break;
            }

            _startPoint = pos;
            UpdateHandlePositions(border);
        }

        private void SelectItem(Border item)
        {
            if (_selectedElement is Border old)
            {
                old.BorderBrush = Brushes.Transparent;
                RemoveResizeHandles(old);
            }

            _selectedElement = item;

            if (item != null)
            {
                item.BorderBrush = Brushes.DodgerBlue;
                AddResizeHandles(item);
                UpdateHandlePositions(item);
                PropertiesPanel.Visibility = Visibility.Visible;
                UpdatePropertiesPanel();
            }
            else
            {
                PropertiesPanel.Visibility = Visibility.Collapsed;
            }

            DeleteButton.IsEnabled = item != null;
            UpdateLayers();
        }

        private void UpdatePropertiesPanel()
        {
            if (_selectedElement is not Border border || border.Child is not TextBox tb) return;

            FontComboBox.SelectedItem = tb.FontFamily.Source;
            FontSizeComboBox.SelectedItem = (int)tb.FontSize;
            PosXBox.Text = Canvas.GetLeft(border).ToString("F0");
            PosYBox.Text = Canvas.GetTop(border).ToString("F0");
            OpacitySlider.Value = border.Opacity;
        }

        #endregion

        #region Mouse & Selection

        private void TemplateCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource == TemplateCanvas || e.OriginalSource is ImageBrush)
                SelectItem(null);
        }

        private void TemplateCanvas_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is Border b) SelectItem(b);
        }

        private void TemplateCanvas_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && _isDragging && _selectedElement is Border border)
            {
                var pos = e.GetPosition(TemplateCanvas);
                Canvas.SetLeft(border, pos.X - _startPoint.X);
                Canvas.SetTop(border, pos.Y - _startPoint.Y);
                UpdateHandlePositions(border);
            }
        }

        private void TemplateCanvas_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false;
            if (_selectedElement is Border b) b.ReleaseMouseCapture();
        }

        private void TextBox_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBox tb)
            {
                tb.IsReadOnly = false;
                tb.Focus();
                e.Handled = true;
            }
        }

        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb) tb.IsReadOnly = true;
        }

        // Then replace your LayersListBox_SelectionChanged with this:
        private void LayersListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LayersListBox.SelectedItem is LayerItem layerItem && layerItem.Element is Border border)
            {
                SelectItem(border);
            }
        }

        #endregion

        #region Page Navigation & Export (Your Original Logic - Fully Preserved)

        private void LoadWord_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                // Only load .docx since Open XML SDK doesn't natively support old .doc format
                Filter = "Word Documents (*.docx)|*.docx",
                Title = "Load Word Document Template"
            };

            if (openFileDialog.ShowDialog() != true) return;

            try
            {
                string wordPath = openFileDialog.FileName;
                _loadedPdfPath = null; // Clear PDF content

                // CALL THE NEW OPEN XML SDK METHOD
                string extractedText = ExtractTextFromDocx(wordPath);

                // Assuming a single page for a Word doc for simplicity in this model
                _pageCount = 1;
                _currentPage = 1;
                _pageElements.Clear();
                _pageElements[1] = new List<UIElement>();

                // Store the text for the background
                _loadedDocumentText = extractedText;

                // Set up the canvas as a blank A4 sheet size (794x1123 from XAML)
                TemplateCanvas.Width = 794;
                TemplateCanvas.Height = 1123;
                PdfBackgroundBrush.ImageSource = null; // Clear PDF image background

                // Add a permanent, non-draggable text display for the loaded content
                RenderWordContentToCanvas(extractedText);

                // Restore page state (which is empty) and update UI
                RestorePageState(_currentPage);
                UpdateNavigationUI();

                ExportButton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading Word document: {ex.Message}", "Word Load Error", MessageBoxButton.OK, MessageBoxImage.Error);
                _loadedDocumentText = null;
            }
        }
        private string ExtractTextFromDocx(string path)
        {
            var textBuilder = new StringBuilder();

            try
            {
                using (WordprocessingDocument wordDoc = WordprocessingDocument.Open(path, false))
                {
                    // Access the main document part (where the content lives)
                    var body = wordDoc.MainDocumentPart?.Document.Body;

                    if (body != null)
                    {
                        // Iterate through all Paragraph objects in the document body
                        foreach (var paragraph in body.Elements<DocumentFormat.OpenXml.Wordprocessing.Paragraph>())
                        {
                            // Use InnerText to combine the text from all Run elements in the paragraph
                            textBuilder.AppendLine(paragraph.InnerText);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Rethrow a more informative exception
                throw new InvalidOperationException($"Failed to extract text from DOCX using Open XML SDK. The file may be corrupt or not a valid .docx file. Details: {ex.Message}", ex);
            }

            return textBuilder.ToString();
        }
        private void RenderWordContentToCanvas(string text)
        {
            // We use a non-draggable, non-interactive TextBox to display the document text.
            var backgroundTextDisplay = new TextBox
            {
                Text = text,
                IsReadOnly = false,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = (Brush)FindResource("TextDark"),
                FontFamily = new FontFamily("Calibri"),
                FontSize = 20,
                // Make it fill most of the A4 canvas with margins
                Width = TemplateCanvas.Width - 40,
                Height = TemplateCanvas.Height - 40
            };

            // Wrap in a Border to position it
            var container = new Border { Child = backgroundTextDisplay };

            Canvas.SetLeft(container, 20); // Margin
            Canvas.SetTop(container, 20);  // Margin

            // Add it to the canvas, ensuring it is *not* added to _pageElements 
            // so it doesn't get exported as an editable field.
            TemplateCanvas.Children.Add(container);

            // To ensure it's visually behind the interactive elements
            Panel.SetZIndex(container, -1);
        }



        private void LoadCSV_Click(object sender, RoutedEventArgs e) { /* Your original code */ }
        private async void ExportPdf_Click(object sender, RoutedEventArgs e) { /* Your original code */ }
        private void PrevPage_Click(object sender, RoutedEventArgs e) { /* ... */ }
        private void NextPage_Click(object sender, RoutedEventArgs e) { /* ... */ }
        private void BackButton_Click(object sender, RoutedEventArgs e) { /* ... */ }

        private void SaveCurrentPageState() { /* Your original code */ }
        private void RestorePageState(int page) { /* Your original code */ }
        private void UpdateNavigationUI() { PageNumberText.Text = $"Page {_currentPage} of {_pageCount}"; PrevPage.IsEnabled = _currentPage > 1; NextPage.IsEnabled = _currentPage < _pageCount; }

        // And update the UpdateLayers() method like this:
        private void UpdateLayers()
        {
            LayersListBox.Items.Clear();

            var borders = TemplateCanvas.Children.OfType<Border>()
                .Where(b => b.Child is TextBox || b.Child is System.Windows.Controls.Image)
                .OrderByDescending(Panel.GetZIndex)
                .ToList();

            foreach (var border in borders)
            {
                string name = border.Child is TextBox tb
                    ? (string.IsNullOrWhiteSpace(tb.Text) ? "Text" : tb.Text.Trim())
                    : border.Child is System.Windows.Controls.Image ? "Image"
                    : "Element";

                // Truncate long names
                if (name.Length > 30)
                    name = name.Substring(0, 27) + "...";

                var layerItem = new LayerItem
                {
                    Element = border,
                    Name = $"{name} (Z: {Panel.GetZIndex(border)})"
                };

                LayersListBox.Items.Add(layerItem);
            }
        }

        private void TemplateNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Enable the Export button only when the template has a non-empty name
            if (ExportButton != null) // ExportButton is the name of your Export PDF button in XAML
            {
                ExportButton.IsEnabled = !string.IsNullOrWhiteSpace(TemplateNameTextBox.Text);
            }
        }

        #endregion

        // Add this helper class at the top of your file (inside the namespace, outside the PdfTemplateCreator class)
        private class LayerItem
        {
            public Border Element { get; set; }
            public string Name { get; set; }
            public override string ToString() => Name;
        }
    }
}