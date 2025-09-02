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
        public LoginWindow()
        {
            InitializeComponent();
            if (Properties.Settings.Default.RememberMe)
            {
                MainWindow main = new MainWindow();
                main.Show();
                this.Close();
            }
        }

        private void btnLogin_Click(object sender, RoutedEventArgs e)
        {
            var userName = txtUsername.Text;
            var password = PasswordHelper.HashPassword(txtPassword.Password);

            var db = new MailMergeDbContext();

            var record = db.Users.Where(x=>userName==x.Email &&  password==x.Password).FirstOrDefault();
            if (record != null)
            {
                if(chkRememberMe.IsChecked == true)
                {
                    Properties.Settings.Default.Username = txtUsername.Text;
                    Properties.Settings.Default.RememberMe = chkRememberMe.IsChecked == true;
                    Properties.Settings.Default.Save();
                }

                MainWindow main = new MainWindow();
                main.Show();
                this.Close();
            }
            else
            {
                txtStatus.Text = "Invalid username or password!";
                txtStatus.Foreground = Brushes.Red;
            }
        }

        private void ForgotPassword_Click(object sender, MouseButtonEventArgs e)
        {

        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
