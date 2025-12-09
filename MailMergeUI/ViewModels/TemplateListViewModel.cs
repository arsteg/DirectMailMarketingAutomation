using MailMerge.Data;
using MailMerge.Data.Models;
using MailMergeUI.Helpers;
using MailMergeUI.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace MailMergeUI.ViewModels
{
    public class TemplateListViewModel : INotifyPropertyChanged
    {
        private readonly MailMergeDbContext  _db; // Replace with your actual DbContext or service

        public ObservableCollection<Template> Templates { get; set; }
        public ObservableCollection<Campaign> Campaigns { get; set; } // To check usage

        private string _statusMessage = "";
        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        public ICommand BackCommand { get; }
        public ICommand AddTemplateCommand { get; }
        public ICommand DeleteTemplateCommand { get; }

        public TemplateListViewModel(MailMergeDbContext dbContext)
        {
            _db = dbContext; // Dependency inject in real app
            Templates = new ObservableCollection<Template>();
            Campaigns = new ObservableCollection<Campaign>();

            LoadData();

            BackCommand = new RelayCommand(_ => GoBack());
            AddTemplateCommand = new RelayCommand(_ => AddNewTemplate());
            DeleteTemplateCommand = new RelayCommand<Template>(DeleteTemplate, CanDeleteTemplate);
        }

        private void LoadData()
        {
            Templates.Clear();
            var templates = _db.Templates.ToList();
            foreach (var t in templates) Templates.Add(t);

            // Load campaigns to check usage
            Campaigns.Clear();
            var campaigns = _db.Campaigns
                .Include("Stages")
                .ToList();
            foreach (var c in campaigns) Campaigns.Add(c);
        }

        public bool CanDeleteTemplate(Template template)
        {
            if (template == null) return false;

            return !Campaigns.Any(c =>
                c.Stages.Any(s => s.TemplateId == template.Id.ToString()));
        }

        private void DeleteTemplate(Template template)
        {
            if (template == null) return;

            if (!CanDeleteTemplate(template))
            {
                StatusMessage = $"Cannot delete '{template.Name}' – it's used in one or more campaigns.";
                return;
            }

            var result = MessageBox.Show(
                $"Are you sure you want to delete template '{template.Name}'?",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _db.Templates.Remove(template);
                _db.SaveChanges();
                Templates.Remove(template);
                StatusMessage = $"Template '{template.Name}' deleted successfully.";
            }
        }

        private void AddNewTemplate()
        {
            // Open file dialog or new template window
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Word Documents (*.docx)|*.docx|All Files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                var newTemplate = new Template
                {
                    Name = System.IO.Path.GetFileName(dialog.FileName),
                    Path = dialog.FileName
                };

                _db.Templates.Add(newTemplate);
                _db.SaveChanges();
                Templates.Add(newTemplate);
                StatusMessage = $"Template '{newTemplate.Name}' added.";
            }
        }

        private void GoBack()
        {
            // Navigate back (e.g., via navigation service or close window)
            Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)?.Close();
            // Or use your navigation framework
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}