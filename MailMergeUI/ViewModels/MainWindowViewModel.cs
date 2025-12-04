using MailMerge.Data;
using MailMerge.Data.Models;
using MailMergeUI.Helpers;
using MailMergeUI.Models;
using MailMergeUI.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using Serilog;
using System.Windows;

namespace MailMergeUI.ViewModels
{
    public class MainWindowViewModel : BaseViewModel
    {
        private object _currentView;
        private readonly ApiService _api = new();
        private readonly PrinterService _printer = new();
        private readonly LogService _log = new();
        private readonly MailMergeDbContext _dbContext;


        public ObservableCollection<Campaign> Campaigns { get; } = new();
        private Campaign _activeCampaign;
        public Campaign ActiveCampaign
        {
            get => _activeCampaign;
            set
            {
                _activeCampaign = value;
                OnPropertyChanged();
                _ = LoadPendingCountAsync();
            }
        }

        private int _pendingLetters;
        public int PendingLetters
        {
            get => _pendingLetters;
            set { _pendingLetters = value; OnPropertyChanged(); }
        }

        private string _status = "Ready";
        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        public ICommand PrintTodayCommand { get; }
        public ICommand RefreshLeadsCommand { get; }



        public object CurrentView
        {
            get => _currentView;
            set { _currentView = value; OnPropertyChanged(); }
        }

        //public DashboardViewModel DashboardVM { get; }
      
        public BlackListViewModel BlacklistVM { get; }
        public SystemLogViewModel LogVM { get; }

        public ICommand ShowDashboardCommand { get; }
        public ICommand ShowCampaignCommand { get; }
        public ICommand ShowBlacklistCommand { get; }
        public ICommand ShowLogCommand { get; }

        public MainWindowViewModel(MailMergeDbContext dbContext)
        {
            _dbContext = dbContext;
            LoadData();
           
            BlacklistVM = new BlackListViewModel(_dbContext);
            LogVM = new SystemLogViewModel();
            //ShowDashboardCommand = new RelayCommand(_ => CurrentView = DashboardVM);
            
            ShowBlacklistCommand = new RelayCommand(_ => CurrentView = BlacklistVM);
            ShowLogCommand = new RelayCommand(_ => CurrentView = LogVM);


            PrintTodayCommand = new RelayCommand(async _ => await PrintTodayAsync(), _ => ActiveCampaign != null);
            RefreshLeadsCommand = new RelayCommand(async _ => await RefreshLeadsAsync(), _ => ActiveCampaign != null);


            //CurrentView = DashboardVM; // Default
        }


        private void LoadData()
        {
            var campaignList = _dbContext.Campaigns.ToList();
            foreach(var campaign in campaignList)
            {
                Campaigns.Add(campaign);
            }
            ActiveCampaign = Campaigns.Any() ? Campaigns.FirstOrDefault() : new Campaign();
        }

        private async Task LoadPendingCountAsync()
        {
            try
            {
                Status = "Searching leads...";
                PendingLetters = await _api.SearchLeadsAsync(ActiveCampaign?.LeadSource.FiltersJson ?? "");
                Status = $"{PendingLetters} letters pending today.";
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
           
        }

        private async Task RefreshLeadsAsync()
        {
            await LoadPendingCountAsync();
            _log.Log($"Refreshed leads for campaign: {ActiveCampaign.Name}");
        }

        private async Task PrintTodayAsync()
        {
            if (PendingLetters == 0) { Status = "No letters to print."; return; }

            Status = "Printing...";
            bool success = true;

            // Simulate printing each letter
            for (int i = 0; i < Math.Min(PendingLetters, 10); i++)
            {
                var settings = ActiveCampaign.LetterPrinter;
                if (settings.IsAutomatic)
                {
                    success &= await _printer.PrintAsync(settings.SelectedPrinter, $"Letter {i + 1}");
                }
                else
                {
                    await _printer.SavePdfAsync(settings.OutputDirectory, $"Letter_{i + 1}.pdf");
                }
            }

            Status = success ? "Print job completed." : "Some print jobs failed.";
            _log.Log(success ? "Print batch succeeded." : "Print batch had errors.");
            PendingLetters = 0;
        }

    }
    }
