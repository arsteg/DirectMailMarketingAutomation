using MailMerge.Data.Models;
using MailMergeUI.Helpers;
using MailMergeUI.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace MailMergeUI.ViewModels
{
    public class CampaignEditViewModel : BaseViewModel
    {
        private readonly CampaignService _service;
        public Campaign Campaign { get; }

        // === EXISTING: Lead Source UI ===
        public ObservableCollection<ScheduleType> ScheduleTypes { get; } = new(Enum.GetValues(typeof(ScheduleType)).Cast<ScheduleType>());
        public ObservableCollection<CheckBoxModel> DayCheckBoxes { get; } = new();
        public ObservableCollection<KeyValuePair<string, TimeSpan>> TimesList { get; } = new();

        public TimeSpan SelectedTime
        {
            get => Campaign.LeadSource.RunAt;
            set { Campaign.LeadSource.RunAt = value; OnPropertyChanged(); }
        }

        public bool IsDaysOfWeekEnabled => Campaign.LeadSource.Type == ScheduleType.Weekly || Campaign.LeadSource.Type == ScheduleType.Monthly;

        // === NEW: Follow-Up Stages ===
        public ObservableCollection<FollowUpStageViewModel> Stages { get; } = new();
        public ObservableCollection<Template> Templates { get; } = new();

        // === Commands ===
        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand AddStageCommand { get; }
        public ICommand RemoveStageCommand { get; }

        public event Action OnSaved;

        public CampaignEditViewModel(Campaign? campaign, CampaignService service)
        {
            _service = service;
            Campaign = campaign ?? new Campaign
            {
                LeadSource = new LeadSource(),
                Stages = new ObservableCollection<FollowUpStage>()
            };

            SetupLeadSource();
            LoadTemplates();
            LoadStages();

            SaveCommand = new RelayCommand(_ => Save());
            CancelCommand = new RelayCommand(_ => CloseWindow());
            AddStageCommand = new RelayCommand(_ => AddStage());
            RemoveStageCommand = new RelayCommand(param => RemoveStage(param as FollowUpStageViewModel));
        }

        private void SetupLeadSource()
        {
            // Days of Week
            foreach (var day in Enum.GetNames(typeof(DayOfWeek)))
            {
                DayCheckBoxes.Add(new CheckBoxModel
                {
                    DisplayName = day,
                    IsChecked = Campaign.LeadSource.DaysOfWeek.Contains(day)
                });
            }

            // Time slots
            for (int h = 0; h < 24; h++)
            {
                TimesList.Add(new KeyValuePair<string, TimeSpan>($"{h:00}:00", TimeSpan.FromHours(h)));
                TimesList.Add(new KeyValuePair<string, TimeSpan>($"{h:00}:30", TimeSpan.FromHours(h) + TimeSpan.FromMinutes(30)));
            }
        }

        private void LoadTemplates()
        {
            // TODO: Replace with real template loading
            Templates.Add(new Template { Id = "1", Name = "Welcome Letter" });
            Templates.Add(new Template { Id = "2", Name = "Follow-Up" });
            Templates.Add(new Template { Id = "3", Name = "Final Notice" });
        }

        private void LoadStages()
        {
            foreach (var stage in Campaign.Stages)
                Stages.Add(new FollowUpStageViewModel(stage));

            if (!Stages.Any())
                AddStage();
        }

        private void AddStage()
        {
            var delay = Stages.Count == 0 ? 0 : 7;
            var stage = new FollowUpStage { StageName = "New Stage", DelayDays = delay };
            Stages.Add(new FollowUpStageViewModel(stage));
        }

        private void RemoveStage(FollowUpStageViewModel? vm)
        {
            if (vm != null)
            {
                Stages.Remove(vm);
                if (Stages.Any()) Stages[0].DelayDays = 0;
            }
        }

        private void Save()
        {
            if (string.IsNullOrWhiteSpace(Campaign.Name))
            {
                MessageBox.Show("Campaign name is required.");
                return;
            }

            if (!Stages.Any())
            {
                MessageBox.Show("At least one stage is required.");
                return;
            }

            // Enforce first stage = 0 days
            Stages[0].DelayDays = 0;

            // Sync back to Campaign.Stages
            Campaign.Stages.Clear();
            foreach (var vm in Stages)
                Campaign.Stages.Add(vm.Model);

            // === SAFE SAVE: Use your existing service ===
            try
            {
                if (Campaign.Id == 0)
                {
                    _service.Campaigns.Add(Campaign);
                }
                else
                {
                    var existing = _service.Campaigns.FirstOrDefault(c => c.Id == Campaign.Id);
                    if (existing != null)
                    {
                        existing.Name = Campaign.Name;
                        existing.LeadSource = Campaign.LeadSource;
                        existing.Stages = Campaign.Stages;
                    }
                }
                _service.SaveCampaign(Campaign); // This must exist in your service

                OnSaved?.Invoke();
                CloseWindow();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Save failed: {ex.Message}");
            }
        }

        private void CloseWindow()
        {
            var window = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.DataContext == this);
            window?.Close();
        }
    }

    // === Helper: Stage ViewModel ===
    public class FollowUpStageViewModel : BaseViewModel
    {
        public FollowUpStage Model { get; }
        public string StageName { get => Model.StageName; set { Model.StageName = value; OnPropertyChanged(); } }
        public string TemplateId { get => Model.TemplateId; set { Model.TemplateId = value; OnPropertyChanged(); } }
        public int DelayDays
        {
            get => Model.DelayDays;
            set
            {
                Model.DelayDays = Math.Max(0, value);
                OnPropertyChanged();
            }
        }

        public FollowUpStageViewModel(FollowUpStage model) => Model = model;
    }

    // === Helper: Checkbox for Days ===
    public class CheckBoxModel : BaseViewModel
    {
        public string DisplayName { get; set; } = "";
        private bool _isChecked;
        public bool IsChecked
        {
            get => _isChecked;
            set { _isChecked = value; OnPropertyChanged(); }
        }
    }

    // === Dummy Template (replace with real one later) ===
    public class Template
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
    }
}