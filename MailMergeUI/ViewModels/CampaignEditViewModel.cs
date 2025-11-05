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
            set { 
                Campaign.LeadSource.RunAt = value;
                OnPropertyChanged();
                // Sync text to selected item
                var item = TimesList.FirstOrDefault(x => x.Value == value);
                if (item.Key != null)
                {
                    ComboBoxText = item.Key; // This triggers validation
                }
            }
        }

        // For dropdown control (optional)
        private bool _isDropDownOpen;
        public bool IsDropDownOpen
        {
            get => _isDropDownOpen;
            set { _isDropDownOpen = value; OnPropertyChanged(); }
        }

        private string _comboBoxText = "";
        public string ComboBoxText
        {
            get => _comboBoxText;
            set
            {
                if (_comboBoxText == value) return;

                _comboBoxText = value;
                OnPropertyChanged();
                if (TimesList == null)
                {
                    return;
                }
                // Try to find matching item
                var match = TimesList.FirstOrDefault(x =>
                    string.Equals(x.Key, value?.Trim(), StringComparison.OrdinalIgnoreCase));

                if (match.Key != null)
                {
                    // Valid: update SelectedTime
                    SelectedTime = match.Value;
                    // Optionally: close dropdown
                    IsDropDownOpen = false;
                }
                else
                {
                    // Invalid: do NOT update SelectedTime
                    // But allow typing (for search)
                    // We'll revert on LostFocus if needed
                }
            }
        }

        // Helper: parse strings like "1h", "30m", "2:30", "90" (minutes), etc.
        private bool TryParseTimeSpan(string input, out TimeSpan result)
        {
            result = default;

            if (string.IsNullOrWhiteSpace(input))
                return false;

            input = input.Trim().ToLower();

            // Try exact match in list first
            var exact = TimesList.FirstOrDefault(x => x.Key.Equals(input, StringComparison.OrdinalIgnoreCase));
            if (exact.Key != null)
            {
                result = exact.Value;
                return true;
            }

            // Custom parsing logic
            var totalMinutes = 0.0;

            // Replace known suffixes
            input = input.Replace("h", ":0").Replace("m", "").Replace("s", "sec");

            var parts = input.Split(new[] { ' ', ':' }, StringSplitOptions.RemoveEmptyEntries);
            try
            {
                foreach (var part in parts)
                {
                    if (double.TryParse(part, out var num))
                        totalMinutes += num;
                    else if (part.Contains("sec"))
                        totalMinutes += num / 60.0;
                }
            }
            catch { return false; }

            result = TimeSpan.FromMinutes(totalMinutes);
            return true;
        }

        // Optional: format TimeSpan for display
        private string FormatTimeSpan(TimeSpan ts)
        {
            if (ts.TotalHours >= 1)
                return $"{ts.TotalHours:F1}h";
            else if (ts.TotalMinutes >= 1)
                return $"{ts.TotalMinutes:F0}m";
            else
                return $"{ts.TotalSeconds:F0}s";
        }

        // Optional: Command to handle Enter press
        public ICommand CommitTypedTimeCommand => new RelayCommand(_ =>
        {
            if (TryParseTimeSpan(ComboBoxText, out var ts))
            {
                SelectedTime = ts;
            }
        });

        //public bool IsDaysOfWeekEnabled => Campaign.LeadSource.Type == ScheduleType.Weekly || Campaign.LeadSource.Type == ScheduleType.Monthly;

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
            TimesList.Clear(); // Optional: clear if re-populating

            for (int h = 0; h < 24; h++)
            {
                for (int m = 0; m < 60; m += 10)
                {
                    var timeSpan = TimeSpan.FromHours(h) + TimeSpan.FromMinutes(m);
                    var display = $"{h:00}:{m:00}"; // e.g., 09:10, 14:30

                    TimesList.Add(new KeyValuePair<string, TimeSpan>(display, timeSpan));
                }
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