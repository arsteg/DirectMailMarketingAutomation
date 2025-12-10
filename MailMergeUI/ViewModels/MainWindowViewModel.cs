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
        private readonly DashboardService _dashboardService;
        private readonly PrinterService _printer = new();
        private readonly LogService _log = new();
        private readonly MailMergeDbContext _dbContext;


        public ObservableCollection<Campaign> Campaigns { get; } = new();
        private Campaign _activeCampaign;
        public Campaign ActiveCampaign
        {
            get => _activeCampaign;
            set{
                if (_activeCampaign != value)
                {
                    _activeCampaign = value;
                    OnPropertyChanged();
                    if (_activeCampaign != null)
                    {
                        _ = LoadPendingCountAsync();
                    }
                }
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

        private int _dueToday;
        public int DueToday
        {
            get => _dueToday;
            set{ _dueToday = value; OnPropertyChanged(); }

        }

        private int _printedToday;
        public int PrintedToday
        {
            get => _printedToday;
            set { _printedToday = value; OnPropertyChanged(); }
        }

        private int _printedThisMonth;
        public int PrintedThisMonth
        {
            get => _printedThisMonth;
            set { _printedThisMonth = value; OnPropertyChanged(); }

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
            _dashboardService = new DashboardService(dbContext);
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
            try
            {
                var campaignList = _dbContext.Campaigns.ToList();
            foreach(var campaign in campaignList)
                {
                    Campaigns.Add(campaign);
                }
                ActiveCampaign = Campaigns.Any() ? Campaigns.FirstOrDefault() : new Campaign();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in LoadData for Campaign");
            }
    
        }

        public async Task LoadPendingCountAsync()
        {
            if (ActiveCampaign == null)
            {
                // Reset all counts if no campaign is selected
                PendingLetters = 0;
                DueToday = 0;
                PrintedToday = 0;
                PrintedThisMonth = 0;
                Status = "No campaign selected.";
                return;
            }

            try
            {
                Status = $"Loading data for {ActiveCampaign.Name}...";

                PendingLetters = await _dashboardService.GetPendingLettersTodayAsync(ActiveCampaign.Id);
                DueToday = await _dashboardService.GetDueLettersTodayAsync(ActiveCampaign.Id);
                PrintedToday = await _dashboardService.GetLettersPrintedTodayAsync(ActiveCampaign.Id);
                PrintedThisMonth = await _dashboardService.GetLettersPrintedThisMonthAsync(ActiveCampaign.Id);

                Status = $"{PendingLetters} letters pending today for {ActiveCampaign.Name}.";
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message);
            }

        }

        private async Task RefreshLeadsAsync()
        {
            try
            {
                await LoadPendingCountAsync();
                _log.Log($"Refreshed leads for campaign: {ActiveCampaign.Name}");
            }
            catch (Exception ex)
            {
            
                Log.Error(ex, "Error in Refreshed leads for campaign");
            }

        }

        private async Task PrintTodayAsync()
        {
            if (PendingLetters == 0) { Status = "No letters to print."; return; }

            Status = "Printing...";
            bool success = true;
            try
            {
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
            catch (Exception ex)
            {
                Log.Error(ex, "Error Print jobs failed.");
            }

        }

    }
    }
