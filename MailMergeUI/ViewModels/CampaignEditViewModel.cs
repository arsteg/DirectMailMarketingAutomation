using MailMerge.Data.Models;
using MailMergeUI.Helpers;
using MailMergeUI.Models;
using MailMergeUI.Services;
using MailMergeUI.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace MailMergeUI.ViewModels
{
    public class CampaignEditViewModel : BaseViewModel
    {
        private readonly CampaignService _service;
        private readonly Campaign _original;
        public Campaign Campaign { get; }
        

        public ObservableCollection<FollowUpStage> Stages => Campaign.Stages;
        public ObservableCollection<LetterTemplate> AvailableTemplates { get; }       

        public ICommand AddStageCommand { get; }
        public ICommand RemoveStageCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        public Action<CampaignEditViewModel>? OnSaved;

        // UI State
        private TimeSpan _selectedTime;
        public TimeSpan SelectedTime
        {
            get => _selectedTime;
            set
            {
                if (_selectedTime != value)
                {
                    _selectedTime = value;
                    OnPropertyChanged(nameof(SelectedTime));
                }
            }
        }


        public List<KeyValuePair<string, TimeSpan>> TimesList { get; } = GenerateTimesList();

        private static List<KeyValuePair<string, TimeSpan>> GenerateTimesList()
        {
            var list = new List<KeyValuePair<string, TimeSpan>>
    {
        new KeyValuePair<string, TimeSpan>("-- Select Time --", TimeSpan.Zero)
    };

            for (int hour = 0; hour < 24; hour++)
            {
                for (int minute = 0; minute < 60; minute += 10)
                {
                    DateTime time = new DateTime(1, 1, 1, hour, minute, 0);
                    string display = time.ToString("hh:mm tt"); // e.g. "01:10 AM"
                    list.Add(new KeyValuePair<string, TimeSpan>(display, time.TimeOfDay));
                }
            }

            return list;
        }

        // Your original property
        public List<DayOfWeek> DaysOfWeek { get; set; } = new();

        // For UI: List of day view models (for binding checkboxes)
        public List<DayCheckBoxViewModel> DayCheckBoxes { get; }

        public ObservableCollection<ScheduleType> ScheduleTypes { get; }
          


        public CampaignEditViewModel(Campaign? campaign, CampaignService service)
        {
            if (service == null) return;
            _service = service;
            _original = campaign ?? new Campaign();
            Campaign = _original;
            //Campaign = new Campaign
            //{
            //    Id = _original.Id,
            //    Name = _original.Name,
            //    LeadSource = new LeadSource
            //    {
            //        ApiUrl = _original.LeadSource.ApiUrl,
            //        ApiKey = _original.LeadSource.ApiKey,
            //        FiltersJson = _original.LeadSource.FiltersJson,
            //        RunAt = _original.LeadSource.RunAt
            //    }
            //    //Stages = _original.Stages.Select(s => new FollowUpStage
            //    //{
            //    //    StageName = s.StageName,
            //    //    TemplateId = s.TemplateId,
            //    //    DelayDays = s.DelayDays
            //    //}).ToList()
            //};
            
            DayCheckBoxes = Enum.GetValues(typeof(DayOfWeek))
                .Cast<DayOfWeek>()
                .Select(day => new DayCheckBoxViewModel(day, DaysOfWeek.Contains(day)))
                .ToList();

            AvailableTemplates = new ObservableCollection<LetterTemplate>(_service.Templates);
            AddStageCommand = new RelayCommand(_ => AddStage());
            RemoveStageCommand = new RelayCommand(s => RemoveStage((FollowUpStage)s!));
            SaveCommand = new RelayCommand(_ => Save());
            CancelCommand = new RelayCommand(_ => CloseWindow());
            ScheduleTypes = new ObservableCollection<ScheduleType>((ScheduleType[])Enum.GetValues(typeof(ScheduleType)));

            if (campaign != null)
            {
                SelectedTime = campaign.LeadSource.RunAt;
                DayCheckBoxes = Enum.GetValues(typeof(DayOfWeek))
                                .Cast<DayOfWeek>()
                                .Select(day => new DayCheckBoxViewModel(
                                    day,
                                    campaign.LeadSource.DaysOfWeek.Contains(day.ToString(), StringComparer.OrdinalIgnoreCase)
                                ))
                                .ToList();
            }
        }

        private void AddStage()
        {
            int nextDelay = Stages.Count == 0 ? 0 : Stages.Max(s => s.DelayDays) + 3;
            Stages.Add(new FollowUpStage
            {
                StageName = $"Stage {Stages.Count + 1}",
                DelayDays = nextDelay
            });
        }

        private void RemoveStage(FollowUpStage stage)
        {
            Stages.Remove(stage);
            RenumberDelays();
        }

        private void RenumberDelays()
        {
            int cumulative = 0;
            foreach (var s in Stages)
            {
                if (Stages.IndexOf(s) == 0) s.DelayDays = 0;
                else s.DelayDays = cumulative;
                cumulative += 3; // or keep user value?
            }
        }

        //private bool CanSave()
        //{
        //    return !string.IsNullOrWhiteSpace(Campaign.Name)
        //        && Uri.TryCreate(Campaign.LeadSource.ApiUrl, UriKind.Absolute, out _)
        //        && Stages.All(s => !string.IsNullOrWhiteSpace(s.TemplateId));
        //}

        private void Save()
        {
            var existing = _service.Campaigns.FirstOrDefault(c => c.Id == Campaign.Id);
            if (existing != null)
            {
                _service.Campaigns.Remove(existing);
            }
            Campaign.LeadSource.DaysOfWeek = DayCheckBoxes.Where(x=>x.IsChecked == true).Select(x=>x.DisplayName).ToList();
            Campaign.LeadSource.RunAt = SelectedTime;
            _service.Campaigns.Add(Campaign);
            _service.SaveCampaign(Campaign);
            OnSaved?.Invoke(this);
            CloseWindow();
        }

        private void CloseWindow()
        {
            Application.Current.Windows.OfType<CampaignEditWindow>().FirstOrDefault()?.Close();
        }
    }
}
