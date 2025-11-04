using MailMerge.Data.Models;
using MailMergeUI.Helpers;
using MailMergeUI.Models;
using MailMergeUI.Services;
using MailMergeUI.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        private string _selectedTime ;
        public string SelectedTime
        {
            get => _selectedTime;
            set
            {
                _selectedTime = value;
                if (TimeSpan.TryParse(value, out var ts))
                    Campaign.LeadSource.RunAt = ts;
                OnPropertyChanged();
            }
        }
        private List<string> _timesList = new List<string>
{
    "12:00 AM","12:10 AM","12:20 AM","12:30 AM","12:40 AM","12:50 AM",
    "01:00 AM","01:10 AM","01:20 AM","01:30 AM","01:40 AM","01:50 AM",
    "02:00 AM","02:10 AM","02:20 AM","02:30 AM","02:40 AM","02:50 AM",
    "03:00 AM","03:10 AM","03:20 AM","03:30 AM","03:40 AM","03:50 AM",
    "04:00 AM","04:10 AM","04:20 AM","04:30 AM","04:40 AM","04:50 AM",
    "05:00 AM","05:10 AM","05:20 AM","05:30 AM","05:40 AM","05:50 AM",
    "06:00 AM","06:10 AM","06:20 AM","06:30 AM","06:40 AM","06:50 AM",
    "07:00 AM","07:10 AM","07:20 AM","07:30 AM","07:40 AM","07:50 AM",
    "08:00 AM","08:10 AM","08:20 AM","08:30 AM","08:40 AM","08:50 AM",
    "09:00 AM","09:10 AM","09:20 AM","09:30 AM","09:40 AM","09:50 AM",
    "10:00 AM","10:10 AM","10:20 AM","10:30 AM","10:40 AM","10:50 AM",
    "11:00 AM","11:10 AM","11:20 AM","11:30 AM","11:40 AM","11:50 AM",
    "12:00 PM"
};

        public List<string> TimesList
        {
            get => _timesList;
            set
            {
                if (_timesList != value)
                {
                    _timesList = value;
                    OnPropertyChanged();
                }
            }
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
            Campaign = new Campaign
            {
                Id = _original.Id,
                Name = _original.Name,
                LeadSource = new LeadSource
                {
                    ApiUrl = _original.LeadSource.ApiUrl,
                    ApiKey = _original.LeadSource.ApiKey,
                    FiltersJson = _original.LeadSource.FiltersJson,
                    RunAt = _original.LeadSource.RunAt
                }
                //Stages = _original.Stages.Select(s => new FollowUpStage
                //{
                //    StageName = s.StageName,
                //    TemplateId = s.TemplateId,
                //    DelayDays = s.DelayDays
                //}).ToList()
            };
            SelectedTime = DateTime.Today.Add(Campaign.LeadSource.RunAt).ToString("hh:mm tt", CultureInfo.InvariantCulture);

            // Initialize all 7 days with checkboxes (initially unchecked)
            DayCheckBoxes = Enum.GetValues(typeof(DayOfWeek))
                .Cast<DayOfWeek>()
                .Select(day => new DayCheckBoxViewModel(day, DaysOfWeek.Contains(day)))
                .ToList();

            AvailableTemplates = new ObservableCollection<LetterTemplate>(_service.Templates);

            SelectedTime = "12:00 PM";// DateTime.Today.Add(Campaign.LeadSource.RunAt).ToString("hh:mm tt", CultureInfo.InvariantCulture);

            AddStageCommand = new RelayCommand(_ => AddStage());
            RemoveStageCommand = new RelayCommand(s => RemoveStage((FollowUpStage)s!));
            SaveCommand = new RelayCommand(_ => Save());
            CancelCommand = new RelayCommand(_ => CloseWindow());
            ScheduleTypes = new ObservableCollection<ScheduleType>((ScheduleType[])Enum.GetValues(typeof(ScheduleType))
      );

           
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
            TimeSpan time = TimeSpan.FromSeconds(0);
            TimeSpan.TryParse(SelectedTime, out time);          

            var existing = _service.Campaigns.FirstOrDefault(c => c.Id == Campaign.Id);
            if (existing != null)
            {
                _service.Campaigns.Remove(existing);
            }
            Campaign.LeadSource.RunAt = time;
            _service.Campaigns.Add(Campaign);
            _service.SaveCampaigns();
            OnSaved?.Invoke(this);
            CloseWindow();
        }

        private void CloseWindow()
        {
            Application.Current.Windows.OfType<CampaignEditWindow>().FirstOrDefault()?.Close();
        }
    }
}
