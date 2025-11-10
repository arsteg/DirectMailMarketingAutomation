using MailMerge.Data;
using Microsoft.EntityFrameworkCore;
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
using System.Windows.Shapes;

namespace MailMergeUI.Views
{
    /// <summary>
    /// Interaction logic for BlacklistView.xaml
    /// </summary>
    public partial class BlacklistView : Window
    {
        private readonly MailMergeDbContext _dbContext;
        public BlacklistView(MailMergeDbContext dbContext)
        {
            _dbContext = dbContext;
            InitializeComponent();
        }
        private void btnBack_Click(object sender, RoutedEventArgs e)
        {
            DashboardWindow dashboardWindow = new DashboardWindow(_dbContext);
            dashboardWindow.WindowState = this.WindowState;
            dashboardWindow.WindowStartupLocation = this.WindowStartupLocation;
            dashboardWindow.Show();
            this.Close();
        }
    }
}
