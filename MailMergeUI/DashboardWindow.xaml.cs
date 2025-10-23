using MailMerge.Data;
using System.Windows;
using System.Windows.Input;

namespace MailMergeUI
{
    public partial class DashboardWindow : Window
    {
        private readonly MailMergeDbContext _dbContext;

        public DashboardWindow(MailMergeDbContext dbContext)
        {
            _dbContext = dbContext;
            InitializeComponent();
        }

        private void OpenMainWindow_Click(object sender, RoutedEventArgs e)
        {
            MailMergeWindow main = new MailMergeWindow(_dbContext);
            main.WindowState = this.WindowState;
            main.Show();
            this.Close();
        }

        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            SettingsWindow settings = new SettingsWindow(_dbContext);
            settings.WindowState = this.WindowState;
            settings.Show();
            this.Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void btnLogout_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.Username = string.Empty;
            Properties.Settings.Default.RememberMe = false;
            Properties.Settings.Default.Save();

            LoginWindow loginWindow = new LoginWindow(_dbContext);
            loginWindow.Show();
            this.Close();
        }

        private void btnTemplate_Click(object sender, RoutedEventArgs e)
        {
            TemplateWindow template = new TemplateWindow(_dbContext);
            template.WindowState = this.WindowState;
            template.Show();
            this.Close();
        }

        private void Grid_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }
    }
}
