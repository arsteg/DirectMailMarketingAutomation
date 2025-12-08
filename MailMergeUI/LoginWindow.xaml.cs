using MailMerge.Data;
using MailMerge.Data.Helpers;
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
    /// Interaction logic for LoginWindow.xaml
    /// </summary>
    public partial class LoginWindow : Window
    {
        private readonly MailMergeDbContext _dbContext;
        private readonly MailMergeEngine.MailMergeEngine _engine;

        public LoginWindow(MailMergeDbContext dbContext)
        {
            _dbContext = dbContext;
            _engine = new MailMergeEngine.MailMergeEngine(dbContext);
            InitializeComponent();
            this.Loaded += LoginWindow_Loaded;   
        }

        private void LoginWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Properties.Settings.Default.RememberMe)
                {
                    Serilog.Log.Information("Auto-login enabled for user: {Username}", Properties.Settings.Default.Username);
                    DashboardWindow dashboard = new DashboardWindow(_dbContext);
                    dashboard.Show();
                    this.Close();
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Error during auto-login");
            }
        }

        private void btnLogin_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var userName = txtUsername.Text;
                var password = txtPassword.Password;
                
                Serilog.Log.Debug("Attempting login for user: {Username}", userName);

                var isValidated = _engine.ValidateUser(userName,password);

                if (isValidated)
                {
                    Serilog.Log.Information("Login successful for user: {Username}", userName);
                    if (chkRememberMe.IsChecked == true)
                    {
                        Properties.Settings.Default.Username = txtUsername.Text;
                        Properties.Settings.Default.RememberMe = chkRememberMe.IsChecked == true;
                        Properties.Settings.Default.Save();
                    }

                    DashboardWindow dashboard = new DashboardWindow(_dbContext);
                    dashboard.Show();
                    this.Close();
                }
                else
                {
                    Serilog.Log.Warning("Login failed for user: {Username}", userName);
                    txtStatus.Text = "Invalid username or password!";
                    txtStatus.Foreground = Brushes.Red;
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Error during login process");
                txtStatus.Text = $"Error Occured ! {ex.Message}";
                txtStatus.Foreground = Brushes.Red;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void txtPassword_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                // Option 1: Call the click handler directly
                btnLogin_Click(sender, e);

                // Option 2: Programmatically "click" the button
                // btnLogin.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
        }

        private void Grid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }
    }
}
