using MailMerge.Data;
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
    /// Interaction logic for CampaignListView.xaml
    /// </summary>
    public partial class CampaignListView : Window
    {
        MailMergeDbContext _dbContext;
        public CampaignListView(MailMergeDbContext dbContext)
        {
            InitializeComponent();
            _dbContext= dbContext;
            this.Loaded += CampaignListView_Loaded; 
        }

        private void CampaignListView_Loaded(object sender, RoutedEventArgs e)
        {
            this.DataContext =  new ViewModels.CampaignListViewModel(_dbContext);
        }

        private void btnBack_Click(object sender, RoutedEventArgs e)
        {
            DashboardWindow dashboardWindow = new DashboardWindow(_dbContext);
            dashboardWindow.WindowState = this.WindowState;
            dashboardWindow.Show();
            this.Close();
        }
    }
}
