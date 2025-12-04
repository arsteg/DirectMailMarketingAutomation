using MailMerge.Data;
using MailMerge.Data.Models;
using MailMergeUI.Helpers;
using MailMergeUI.Services;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Xml;
using static System.Windows.Forms.AxHost;
using Serilog;

namespace MailMergeUI.ViewModels
{
    public class CampaignEditViewModel : BaseViewModel
    {
        private readonly CampaignService _service;
        private readonly MailMergeDbContext _dbContext;
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

        private string _cityString;
        public string City
        {
            get => _cityString;
            set { _cityString = value; OnPropertyChanged(); }
        }

        private string _outputPath;
        public string OutputPath
        {
            get => _outputPath;
            set { _outputPath = value; OnPropertyChanged(); }
        }

        private string _stateString;
        public string State
        {
            get => _stateString;
            set { _stateString = value; OnPropertyChanged(); }
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
        public ObservableCollection<TempLateViewModel> Templates { get; } = new();

        // === Commands ===
        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand AddStageCommand { get; }
        public ICommand RemoveStageCommand { get; }
        public ICommand BrowseOutputPathCommand { get; }

        public event Action OnSaved;

        private string _selectedPrinter;
        public string SelectedPrinter
        {
            get => _selectedPrinter;
            set
            {
                _selectedPrinter = value;
                OnPropertyChanged();
            }
        }
        public CampaignEditViewModel(Campaign? campaign, CampaignService service,MailMergeDbContext dbContext)
        {
            try {
                _dbContext = dbContext;
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
                BrowseOutputPathCommand = new RelayCommand(_ => BrowseOutputPath());
                RemoveStageCommand = new RelayCommand(param => RemoveStage(param as FollowUpStageViewModel));

                if (campaign != null)
                {
                    SelectedTime = campaign.LeadSource.RunAt;
                    DayCheckBoxes = new ObservableCollection<CheckBoxModel>(
        Enum.GetValues(typeof(DayOfWeek))
            .Cast<DayOfWeek>()
            .Select(day => new CheckBoxModel
            {
                DisplayName = day.ToString(),
                IsChecked = Campaign.LeadSource.DaysOfWeek.Contains(day.ToString())
            })
    );
                    OutputPath = campaign.OutputPath;
                    // Load saved printer
                    SelectedPrinter = campaign.Printer ?? SelectedPrinter;
                    (this.State, this.City) = SearchCriteriaHelper.GetStateAndCityFromJson(campaign.LeadSource.FiltersJson);

                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error initializing CampaignEditViewModel");
            } 
        }

        private void BrowseOutputPath()
        {

            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select output folder";
                dialog.UseDescriptionForTitle = true;
                dialog.ShowNewFolderButton = true;

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    Campaign.OutputPath = dialog.SelectedPath;
                    this.OutputPath = Campaign.OutputPath;
                }
            }
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
            var templates = _dbContext.Templates.ToList();

            foreach (var template in templates)
            {
                var obj = new TempLateViewModel
                {
                    Id = template.Id.ToString(),
                    Path = template.Path,
                    Name = template.Name
                };

                Templates.Add(obj);
            }
        }

        //private void LoadStages()
        //{
        //    foreach (var stage in Campaign.Stages)
        //        Stages.Add(new FollowUpStageViewModel(stage));

        //    if (!Stages.Any())
        //        AddStage();
        //}

        private void LoadStages()
        {
            Stages.Clear(); // Add this
            foreach (var stage in Campaign.Stages)
            {
                var vm = new FollowUpStageViewModel(stage);
                Stages.Add(vm);
                // Force UI update
                OnPropertyChanged(nameof(FollowUpStageViewModel.DelayDays));
            }
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
                System.Windows.MessageBox.Show("Campaign name is required.");
                return;
            }

            var locationValidator = LocationValidator.ValidateLocation(State, City);

            if (locationValidator.Item1==false)
            {
                System.Windows.MessageBox.Show(locationValidator.Item2);
                return;
            }

            if (!Stages.Any())
            {
                System.Windows.MessageBox.Show("At least one stage is required.");
                return;
            }

            // Enforce first stage = 0 days
            if (Stages.Count > 0 && Stages[0].DelayDays != 0)
                Stages[0].DelayDays = 0; // Only enforce if invalid

            // Sync back to Campaign.Stages
            Campaign.Stages.Clear();
            foreach (var vm in Stages)
                Campaign.Stages.Add(vm.Model);

            // === SAFE SAVE: Use your existing service ===
            try
            {
                Campaign.LeadSource.FiltersJson = SearchCriteriaHelper.BuildSearchCriteriaJson(State, City);

                //Campaign.LeadSource.FiltersJson = JsonConvert.SerializeObject(searchCriteriaBody, Newtonsoft.Json.Formatting.Indented);
                
                Campaign.LeadSource.DaysOfWeek = DayCheckBoxes.Where(x=>x.IsChecked == true).Select(x=>x.DisplayName).ToList();
               
                // Save printer from ViewModel property (NOT from UI element)
                Campaign.Printer = SelectedPrinter ?? string.Empty;

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
                        existing.Printer = Campaign.Printer;
                    }
                }
                _service.SaveCampaign(Campaign); // This must exist in your service

                OnSaved?.Invoke();
                CloseWindow();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Save failed: {ex.Message}");
                Log.Error(ex, "Error saving campaign");
            }
        }

        private void CloseWindow()
        {
            var window = System.Windows.Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.DataContext == this);
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
                if (Model.DelayDays != value)
                {
                    Model.DelayDays = Math.Max(0, value);
                    OnPropertyChanged();
                }
            }
        }

        public FollowUpStageViewModel(FollowUpStage model)
        {
            Model = model;
            OnPropertyChanged(nameof(DelayDays)); // Add this
            OnPropertyChanged(nameof(StageName));
            OnPropertyChanged(nameof(TemplateId));
        }
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
    public class TempLateViewModel
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Path { get; set; } = "";
    }
}