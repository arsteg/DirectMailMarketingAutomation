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
using System.Windows.Shapes;

namespace MailMergeUI
{
    /// <summary>
    /// Interaction logic for TemplateWindow.xaml
    /// </summary>
    public partial class TemplateWindow : Window
    {
        private readonly MailMergeDbContext _dbContext;

        public TemplateWindow(MailMergeDbContext dbContext)
        {
            _dbContext = dbContext;
            InitializeComponent();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void btnBack_Click(object sender, RoutedEventArgs e)
        {
            // If you want to return to MainWindow
            DashboardWindow dashboard = new DashboardWindow(_dbContext);
            dashboard.WindowState = this.WindowState;
            dashboard.Show();
            this.Close();
        }

        private void TemplateGrid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void btnLoadTemplate_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
