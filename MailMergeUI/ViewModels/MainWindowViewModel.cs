using MailMerge.Data;
using MailMerge.Data.Models;
using MailMergeUI.Helpers;
using MailMergeUI.Services;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

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
            set
            {
                if (_activeCampaign != value)
                {
                    _activeCampaign = value;
                    OnPropertyChanged();
                    if (_activeCampaign != null)
                    {
                        //_ = LoadPendingCountAsync();
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
            set { _dueToday = value; OnPropertyChanged(); }

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
                foreach (var campaign in campaignList)
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
                //Status = $"Loading data for {ActiveCampaign.Name}...";

                //PendingLetters = await _dashboardService.GetPendingLettersTodayAsync(ActiveCampaign.Id);
                //DueToday = await _dashboardService.GetDueLettersTodayAsync(ActiveCampaign.Id);
                //PrintedToday = await _dashboardService.GetLettersPrintedTodayAsync(ActiveCampaign.Id);
                //PrintedThisMonth = await _dashboardService.GetLettersPrintedThisMonthAsync(ActiveCampaign.Id);

                //Status = $"{PendingLetters} letters pending today for {ActiveCampaign.Name}.";
                string RequestedFields = "RadarID,APN,PType,Address,City,State,ZipFive,Owner,OwnershipType,PrimaryName,PrimaryFirstName,OwnerAddress,OwnerCity,OwnerZipFive,OwnerState,inForeclosure,ForeclosureStage,ForeclosureDocType,ForeclosureRecDate,isSameMailing,Trustee,TrusteePhone,TrusteeSaleNum";
                string url = "https://api.propertyradar.com/v1/properties";
                string? bearerToken = System.Configuration.ConfigurationManager.AppSettings["API Key"];

                if (string.IsNullOrEmpty(bearerToken))
                {
                    Log.Warning("API Key missing in configuration.");
                }

                string rawQueryParams = $"?Purchase=1&Fields={RequestedFields}";
                var _httpClient = new HttpClient();
                //await context.Database.EnsureCreatedAsync();
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

                int start = 0;
                int batchSize = 500;
                int totalResults = 0;
                bool moreData = true;

                do
                {
                    // Build URL with pagination
                    var pagedUrl = $"{url}{rawQueryParams}&Start={start}";
                    Log.Debug("Fetching records starting from {Start}", start);

                    // Serialize request body
                    var jsonContent = ActiveCampaign.LeadSource.FiltersJson;
                    var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                    // Send POST
                    var response = await _httpClient.PostAsync(pagedUrl, content);
                    if (!response.IsSuccessStatusCode)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        Log.Error("API Call Failed at start={Start}. Status: {StatusCode}. Details: {Details}", start, response.StatusCode, errorContent);
                        break;
                    }

                    // Deserialize response
                    var responseStream = await response.Content.ReadAsStreamAsync();
                    var apiResponse = await JsonSerializer.DeserializeAsync<ApiResponse>(responseStream);

                    if (apiResponse?.Results == null || !apiResponse.Results.Any())
                    {
                        Log.Information("No results found for batch starting at {Start}", start);
                        break;
                    }

                    totalResults = apiResponse.TotalResultCount;

                    // Map results
                    var propertiesToSave = apiResponse.Results
                        .Select(dto => MapToPropertyRecord(dto))
                        .ToList();

                    propertiesToSave.ForEach(x => x.CampaignId = ActiveCampaign.Id);

                    var radarIds = propertiesToSave.Select(p => p.RadarId).ToList();

                    // Fetch existing records with matching RadarIds
                    var existingRecords = await _dbContext.Properties
                        .Where(p => radarIds.Contains(p.RadarId))
                        .ToListAsync();

                    // Determine new records (those not in DB)
                    var existingRadarIds = existingRecords.Select(p => p.RadarId).ToList();
                    var newProperties = propertiesToSave
                        .Where(p => !existingRadarIds.Contains(p.RadarId))
                        .ToList();

                    // ✅ Update CampaignId for all existing records
                    foreach (var existing in existingRecords)
                    {
                        existing.CampaignId = ActiveCampaign.Id;
                    }

                    // ✅ Add new records
                    if (newProperties.Any())
                    {
                        await _dbContext.Properties.AddRangeAsync(newProperties);
                        int saved = await _dbContext.SaveChangesAsync();
                        Log.Information("Saved {Count} new properties.", saved);
                    }
                    else
                    {
                        Log.Debug("No new records to insert for batch starting {Start}.", start);
                    }
                    await _dbContext.SaveChangesAsync();

                    start += batchSize;
                    moreData = start < totalResults;

                } while (moreData);

                ActiveCampaign.LastRunningTime = DateTime.Now;
                await _dbContext.SaveChangesAsync();

            }
            catch (Exception ex)
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

        private PropertyRecord MapToPropertyRecord(PropertyResultDto dto)
        {
            // Mapping logic must be updated to include the new fields from the URL
            return new PropertyRecord
            {
                RadarId = dto.RadarID ?? string.Empty,
                Apn = dto.APN ?? string.Empty,
                Type = dto.PType ?? string.Empty,
                Address = dto.Address ?? string.Empty,
                City = dto.City ?? string.Empty,
                State = dto.State ?? string.Empty,
                Zip = dto.ZipFive ?? string.Empty,
                OwnerOcc = dto.IsSameMailing.HasValue && dto.IsSameMailing.Value == 1 ? "1" : "0",

                Owner = dto.Owner ?? string.Empty,
                OwnerType = dto.OwnershipType ?? string.Empty,
                PrimaryName = dto.PrimaryName ?? string.Empty,
                PrimaryFirst = dto.PrimaryFirstName ?? string.Empty,

                MailAddress = dto.OwnerAddress ?? string.Empty,
                MailCity = dto.OwnerCity ?? string.Empty,
                MailState = dto.OwnerState ?? string.Empty,
                MailZip = dto.OwnerZipFive ?? string.Empty,

                Foreclosure = dto.InForeclosure.HasValue && dto.InForeclosure.Value == 1 ? "1" : "0",

                // NEW FIELDS based on the URL provided
                FclStage = dto.ForeclosureStage ?? string.Empty,
                FclDocType = dto.ForeclosureDocType ?? string.Empty,
                FclRecDate = dto.ForeclosureRecDate ?? string.Empty,
                Trustee = dto.Trustee ?? string.Empty,
                TrusteePhone = dto.TrusteePhone ?? string.Empty,
                TsNumber = dto.TrusteeSaleNum ?? string.Empty
            };
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
