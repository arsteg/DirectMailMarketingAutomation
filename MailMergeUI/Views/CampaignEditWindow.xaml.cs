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
    }
}
