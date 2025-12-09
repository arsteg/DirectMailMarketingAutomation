using MailMerge.Data;
using MailMerge.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace MailMergeUI.Views
{
    public partial class TemplateListView : Window
    {
        private List<Template> AllTemplates;
        private List<Campaign> AllCampaigns;
        MailMergeDbContext _dbContext;
        public TemplateListView(MailMergeDbContext dbContext)
        {
            InitializeComponent();
            this._dbContext = dbContext;
            Loaded += TemplateListView_Loaded;
        }

        private void TemplateListView_Loaded(object sender, RoutedEventArgs e)
        {
            LoadTemplates();
        }

        private void LoadTemplates()
        {          

            AllTemplates = _dbContext.Templates.ToList();
            AllCampaigns = _dbContext.Campaigns
                .Include(c => c.Stages)
                .ToList();

            var templatesWithUsage = AllTemplates.Select(t => new TemplateViewModel
            {
                Id = t.Id,
                Name = t.Name,
                Path = t.Path,
                IsInUse = AllCampaigns.Any(c =>
                    c.Stages.Any(s => s.TemplateId == t.Id.ToString()))
            }).ToList();

            dgTemplates.ItemsSource = templatesWithUsage;
            lblStatus.Text = $"{templatesWithUsage.Count} template(s) loaded.";
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            DashboardWindow dashboardWindow = new DashboardWindow(_dbContext);
            dashboardWindow.WindowState = this.WindowState;
            dashboardWindow.Show();
            this.Close();
        }

        private void BtnAddTemplate_Click(object sender, RoutedEventArgs e)
        {
            TemplateWindow template = new TemplateWindow(_dbContext);
            template.WindowState = this.WindowState;
            template.Show(); 
            this.Close();
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var vm = button?.Tag as TemplateViewModel;
            if (vm == null) return;

            var template = AllTemplates.FirstOrDefault(t => t.Id == vm.Id);
            if (template == null) return;

            if (vm.IsInUse)
            {
                MessageBox.Show(
                    $"Cannot delete template '{template.Name}' because it is used in one or more campaigns.",
                    "Cannot Delete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Stop);
                return;
            }

            var result = MessageBox.Show(
                $"Delete template '{template.Name}'?\n\nPath: {template.Path}",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {

                _dbContext.Templates.Remove(template);
                _dbContext.SaveChanges();

                LoadTemplates();
                lblStatus.Text = $"Deleted: {template.Name}";
            }
        }
    }

    // Simple view model just for DataGrid binding (no INotify needed)
    public class TemplateViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Path { get; set; }
        public bool IsInUse { get; set; }
    }
}