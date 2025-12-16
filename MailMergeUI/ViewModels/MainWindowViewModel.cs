using MailMerge.Data;
using MailMerge.Data.Models;
using MailMergeUI.Helpers;
using MailMergeUI.Models;
using MailMergeUI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace MailMergeUI.ViewModels
{
    public class MainWindowViewModel : BaseViewModel
    {
        private object _currentView;
        private readonly DashboardService _dashboardService;
        private readonly PrinterService _printer = new();
        private readonly LogService _log = new();
        private readonly MailMergeDbContext _dbContext;
        private readonly ApiService _apiService;
        private readonly MailMergeEngine.MailMergeEngine _mailMergeEngine;
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


        private int _pendingLettersFromApi;
        public int PendingLettersFromApi
        {
            get => _pendingLettersFromApi;
            set { _pendingLettersFromApi = value; OnPropertyChanged(); }
        }

        private int _dueTomorrow;
        public int DueTomorrow
        {
            get => _dueTomorrow;
            set { _dueTomorrow = value; OnPropertyChanged(); }
        }

        private int _apiPropertyCount;
        public int ApiPropertyCount
        {
            get => _apiPropertyCount;
            set { _apiPropertyCount = value; OnPropertyChanged(); }
        }

        //public DashboardViewModel DashboardVM { get; }

        public BlackListViewModel BlacklistVM { get; }
        public SystemLogViewModel LogVM { get; }

        public ICommand ShowDashboardCommand { get; }
        public ICommand ShowCampaignCommand { get; }
        public ICommand ShowBlacklistCommand { get; }
        public ICommand ShowLogCommand { get; }
        private int totalResults = 0;
        public MainWindowViewModel(MailMergeDbContext dbContext)
        {
            _dashboardService = new DashboardService(dbContext);
            _dbContext = dbContext;
            _mailMergeEngine = App.Services!.GetRequiredService<MailMergeEngine.MailMergeEngine>();
            _apiService = new ApiService(_mailMergeEngine, dbContext);
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
                string useApiSimulator = System.Configuration.ConfigurationManager.AppSettings["USeAPISimulator"];
                if (useApiSimulator?.ToLower() == "true")
                {

                    var csvPath = GetCsvPathFromDataFolder();

                    var csvData = await ReadPropertiesFromCsv(csvPath, ActiveCampaign.Id);
                    if (!csvData.Any())
                    {
                        Log.Warning("no any csvData.");
                        return;
                    }


                    totalResults= await SaveCsvPropertiesAsync(ActiveCampaign, csvData);
                }
                else
                {

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
                  //  int totalResults = 0;
                     totalResults = 0;
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



                Status = $"Loading data for {ActiveCampaign.Name}...";

                PendingLetters = await _dashboardService.GetPendingLettersTodayAsync(ActiveCampaign.Id);
                DueToday = await _dashboardService.GetDueLettersTodayAsync(ActiveCampaign.Id);
               
                PrintedToday = await _dashboardService.GetLettersPrintedTodayAsync(ActiveCampaign.Id);
                PrintedThisMonth = await _dashboardService.GetLettersPrintedThisMonthAsync(ActiveCampaign.Id);

                // NEW: Get property count from API
            //    ApiPropertyCount = await _apiService.GetCampaignPropertyCountFromApiAsync(ActiveCampaign);

                // Get today's pending
                PendingLettersFromApi = await _dashboardService.GetPendingLettersTodayFromApiAsync(
                                      ActiveCampaign.Id, totalResults);

                // Get total due by tomorrow
                DueTomorrow = await _dashboardService.GetDueTomorrowFromApiAsync(
                                 ActiveCampaign.Id, totalResults);

                Status = $"{PendingLettersFromApi} letters pending today for {ActiveCampaign.Name}.";

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


        // here new code for read csv
        private async Task<List<PropertyRecord>> ReadPropertiesFromCsv(string csvFilePath, int campaignId)
        {
            var properties = new List<PropertyRecord>();

            try
            {
                if (!File.Exists(csvFilePath))
                {
                    Log.Error($"CSV file not found at: {csvFilePath}");
                    return properties;
                }

                using (var reader = new StreamReader(csvFilePath))
                {
                    // Read header
                    string headerLine = await reader.ReadLineAsync();
                    if (headerLine == null)
                    {
                        Log.Warning("CSV file is empty");
                        return properties;
                    }

                    var headers = headerLine.Split(',').Select(h => h.Trim()).ToArray();

                    // Read data rows
                    while (!reader.EndOfStream)
                    {
                        var line = await reader.ReadLineAsync();
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        var values = ParseCsvLine(line);

                        var property = new PropertyRecord
                        {
                            CampaignId = campaignId
                        };

                        for (int i = 0; i < headers.Length && i < values.Length; i++)
                        {
                            string header = headers[i].ToLower();
                            string value = values[i].Trim().Trim('"');

                            // Map CSV columns to PropertyRecord properties
                            switch (header)
                            {
                                case "radarid":
                                    property.RadarId = value;
                                    break;
                                case "apn":
                                    property.Apn = value;
                                    break;
                                case "ptype":
                                case "type":
                                    property.Type = value;
                                    break;
                                case "address":
                                    property.Address = value;
                                    break;
                                case "city":
                                    property.City = value;
                                    break;
                                case "state":
                                    property.State = value;
                                    break;
                                case "zipfive":
                                case "zip":
                                    property.Zip = value;
                                    break;
                                case "owner":
                                    property.Owner = value;
                                    break;
                                case "ownershiptype":
                                case "ownertype":
                                    property.OwnerType = value;
                                    break;
                                case "primaryname":
                                    property.PrimaryName = value;
                                    break;
                                case "primaryfirstname":
                                case "primaryfirst":
                                    property.PrimaryFirst = value;
                                    break;
                                case "owneraddress":
                                case "mailaddress":
                                    property.MailAddress = value;
                                    break;
                                case "ownercity":
                                case "mailcity":
                                    property.MailCity = value;
                                    break;
                                case "ownerstate":
                                case "mailstate":
                                    property.MailState = value;
                                    break;
                                case "ownerzipfive":
                                case "mailzip":
                                    property.MailZip = value;
                                    break;
                                case "issamemailing":
                                case "ownerocc":
                                    property.OwnerOcc = value == "1" || value.ToLower() == "true" ? "1" : "0";
                                    break;
                                case "inforeclosure":
                                case "foreclosure":
                                    property.Foreclosure = value == "1" || value.ToLower() == "true" ? "1" : "0";
                                    break;
                                case "foreclosurestage":
                                case "fclstage":
                                    property.FclStage = value;
                                    break;
                                case "foreclosuredoctype":
                                case "fcldoctype":
                                    property.FclDocType = value;
                                    break;
                                case "foreclosurerecdate":
                                case "fclrecdate":
                                    property.FclRecDate = value;
                                    break;
                                case "trustee":
                                    property.Trustee = value;
                                    break;
                                case "trusteephone":
                                    property.TrusteePhone = value;
                                    break;
                                case "trusteesalenum":
                                case "tsnumber":
                                    property.TsNumber = value;
                                    break;
                            }
                        }

                        properties.Add(property);
                    }
                }

                Log.Information($"Loaded {properties.Count} properties from CSV file");
                return properties;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error reading CSV file: {FilePath}", csvFilePath);
                return properties;
            }
        }


        private async Task<int> SaveCsvPropertiesAsync(
            Campaign campaign,
            List<PropertyRecord> csvProperties)
        {
            try
            {
                if (campaign == null)
                {
                    Log.Warning("SaveCsvPropertiesAsync called with null Campaign");
                    return 0;
                }

                if (csvProperties == null || !csvProperties.Any())
                {
                    Log.Warning("No CSV properties provided to save for Campaign {CampaignName}", campaign.Name);
                    return 0;
                }

                var radarIds = csvProperties
                    .Where(p => !string.IsNullOrWhiteSpace(p.RadarId))
                    .Select(p => p.RadarId)
                    .ToList();

                var existing = await _dbContext.Properties
                    .Where(p => radarIds.Contains(p.RadarId))
                    .ToListAsync();

                // Update existing records
                foreach (var e in existing)
                {
                    e.CampaignId = campaign.Id;
                }

                var existingIds = existing
                    .Select(e => e.RadarId)
                    .ToHashSet();

                var newRecords = csvProperties
                    .Where(p => !string.IsNullOrWhiteSpace(p.RadarId) &&
                                !existingIds.Contains(p.RadarId))
                    .ToList();

                if (newRecords.Any())
                {
                    await _dbContext.Properties.AddRangeAsync(newRecords);
                }

                await _dbContext.SaveChangesAsync();

                Log.Information(
                    "CSV save successful. Campaign={Campaign}, New={NewCount}, Existing={ExistingCount}",
                    campaign.Name,
                    newRecords.Count,
                    existing.Count);

                return csvProperties.Count;
            }
            catch (DbUpdateException dbEx)
            {
                Log.Error(
                    dbEx,
                    "Database update error while saving CSV properties for Campaign {CampaignName}",
                    campaign?.Name);

                return 0;
            }
            catch (Exception ex)
            {
                Log.Error(
                    ex,
                    "Unexpected error in SaveCsvPropertiesAsync for Campaign {CampaignName}",
                    campaign?.Name);

                return 0;
            }
        }

        private string GetCsvPathFromDataFolder()
        {
            return Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Data",
                "properties_5000_records.csv"
            );
        }

        // ============================================================
        // 🔹 CSV PARSER (UNCHANGED)
        // ============================================================
        private string[] ParseCsvLine(string line)
        {
            var values = new List<string>();
            var current = new StringBuilder();
            bool inQuotes = false;

            foreach (char c in line)
            {
                if (c == '"') inQuotes = !inQuotes;
                else if (c == ',' && !inQuotes)
                {
                    values.Add(current.ToString());
                    current.Clear();
                }
                else current.Append(c);
            }

            values.Add(current.ToString());
            return values.ToArray();
        }

    }
}
