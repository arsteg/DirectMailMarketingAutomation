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

namespace MailMergeUI.Dialogs
{
    /// <summary>
    /// Interaction logic for ReasonInputDialog.xaml
    /// </summary>
    public partial class ReasonInputDialog : Window
    {
        public string Reason { get; set; } = string.Empty;

        public ReasonInputDialog() => InitializeComponent();

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            Reason = txtReason.Text.Trim();
            DialogResult = true;
        }
    }
}
