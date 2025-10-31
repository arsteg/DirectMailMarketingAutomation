using MailMergeUI.Helpers;
using MailMergeUI.Models;
using MailMergeUI.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

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
        private string _selectedTime = "06:00";
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

            AvailableTemplates = new ObservableCollection<LetterTemplate>(_service.Templates);

            SelectedTime = Campaign.LeadSource.RunAt.ToString(@"hh\:mm");

            AddStageCommand = new RelayCommand(_ => AddStage());
            RemoveStageCommand = new RelayCommand(s => RemoveStage((FollowUpStage)s!));
            SaveCommand = new RelayCommand(_ => Save(), _ => CanSave());
            CancelCommand = new RelayCommand(_ => CloseWindow());
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

        private bool CanSave()
        {
            return !string.IsNullOrWhiteSpace(Campaign.Name)
                && Uri.TryCreate(Campaign.LeadSource.ApiUrl, UriKind.Absolute, out _)
                && Stages.All(s => !string.IsNullOrWhiteSpace(s.TemplateId));
        }

        private void Save()
        {
            var existing = _service.Campaigns.FirstOrDefault(c => c.Id == Campaign.Id);
            if (existing != null)
            {
                _service.Campaigns.Remove(existing);
            }
            _service.Campaigns.Add(Campaign);
            _service.SaveCampaigns();
            OnSaved?.Invoke(this);
            CloseWindow();
        }

        private void CloseWindow()
        {
            //Application.Current.Windows.OfType<CampaignEditWindow>().FirstOrDefault()?.Close();
        }
    }
}
