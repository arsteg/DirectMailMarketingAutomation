using MailMerge.Data;
using MailMergeUI.Properties;
using System.Windows;

namespace MailMergeUI
{
    public partial class SettingsWindow : Window
    {
        private readonly MailMergeDbContext _dbContext;

        public SettingsWindow(MailMergeDbContext dbContext)
        {
            _dbContext = dbContext;
            InitializeComponent();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void btnDarkModeToggle_Click(object sender, RoutedEventArgs e)
        {
            ThemeManager.ToggleTheme();

            if (Properties.Settings.Default.IsDarkMode)
            {
                btnDarkModeToggle.Content = "☀ Light Mode";
            }
            else
            {
                btnDarkModeToggle.Content = "🌙 Dark Mode";
            }
            
        }

        private void btnBack_Click(object sender, RoutedEventArgs e)
        {
            // If you want to return to MainWindow
            DashboardWindow dashboard = new DashboardWindow(_dbContext);
            dashboard.Show();
            this.Close();
        }
    }
}
