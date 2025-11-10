using MailMerge.Data;
using MailMergeUI.ViewModels;
using MailMergeUI.Views;
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
            this.Loaded += DashboardWindow_Loaded;            
        }

        private void DashboardWindow_Loaded(object sender, RoutedEventArgs e)
        {
            this.DataContext = new MainWindowViewModel(_dbContext);
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

        private void btnShowBlacklisted_Click(object sender, RoutedEventArgs e)
        {
            BlacklistView blacklistView = new BlacklistView(this._dbContext);
            blacklistView.WindowState = this.WindowState;
            blacklistView.WindowStartupLocation = this.WindowStartupLocation;
            blacklistView.Show();
            this.Close();
        }

        private void btnCampaign_Click(object sender, RoutedEventArgs e)
        {
            CampaignListView campaignListView = new CampaignListView(this._dbContext);
            campaignListView.WindowState = this.WindowState;
            campaignListView.WindowStartupLocation = this.WindowStartupLocation;
            campaignListView.Show();
            this.Close();
        }
    }
}
