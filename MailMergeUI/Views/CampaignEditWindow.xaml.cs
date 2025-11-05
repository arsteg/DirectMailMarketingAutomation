using MailMergeUI.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MailMergeUI.Views
{
    /// <summary>
    /// Interaction logic for CampaignEditWindow.xaml
    /// </summary>
    public partial class CampaignEditWindow : Window
    {
        public CampaignEditWindow(CampaignEditViewModel viewModel)
        {
            InitializeComponent();
            this.DataContext = viewModel;
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.DataContext != null) {
                                var vm = (CampaignEditViewModel)this.DataContext;
                if (vm != null)
                {
                    if (e.AddedItems.Count > 0 && e.AddedItems[0]?.ToString() =="None")
                    {
                        daysOfWeekItemControl.Visibility= Visibility.Visible;
                        daysOfWeekLabel.Visibility = Visibility.Visible;

                    }
                    else
                    {
                        daysOfWeekItemControl.Visibility = Visibility.Collapsed;
                        daysOfWeekLabel.Visibility = Visibility.Collapsed;
                    }
                }
            }
        }
        // NEW: Allow only numbers in Delay Days
        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !int.TryParse(e.Text, out _);
        }
        private void ComboBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var comboBox = (ComboBox)sender;
            var viewModel = (CampaignEditViewModel)comboBox.DataContext;

            // If current text doesn't match any item, revert to selected
            var match = viewModel.TimesList.Any(x =>
                string.Equals(x.Key, comboBox.Text.Trim(), StringComparison.OrdinalIgnoreCase));

            if (!match && viewModel.SelectedTime != default)
            {
                var selectedItem = viewModel.TimesList.First(x => x.Value == viewModel.SelectedTime);
                comboBox.Text = selectedItem.Key;
            }
        }
        private void ComboBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var comboBox = (ComboBox)sender;
                var text = comboBox.Text.Trim();

                var match = ((CampaignEditViewModel)comboBox.DataContext).TimesList
                    .FirstOrDefault(x => string.Equals(x.Key, text, StringComparison.OrdinalIgnoreCase));

                if (match.Key != null)
                {
                    comboBox.Text = match.Key;
                    comboBox.SelectedValue = match.Value;
                }
                else
                {
                    // Revert to current selected
                    var vm = (CampaignEditViewModel)comboBox.DataContext;
                    if (vm.SelectedTime != default)
                    {
                        var item = vm.TimesList.First(x => x.Value == vm.SelectedTime);
                        comboBox.Text = item.Key;
                    }
                }

                e.Handled = true;
            }
        }
    }
}
