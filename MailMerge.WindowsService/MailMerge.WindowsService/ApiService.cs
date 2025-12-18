using MailMerge.Data;
using MailMerge.Data.Models;
using MailMergeEngine;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Serilog;
using Syncfusion.DocIO.DLS;
using Syncfusion.DocToPDFConverter;
public class ApiService
{
    private readonly MailMergeEngine.MailMergeEngine _engine;
    private readonly MailMergeDbContext _context;
    private string lastTempPdfPath = string.Empty; // store last preview temp file to clean up later

    public ApiService(MailMergeEngine.MailMergeEngine engine,MailMergeDbContext dbContext)
    {
        _context = dbContext;
        _engine = engine;
    }

    /// <summary>
    /// Performs a POST API call using the specific PropertyRadar criteria structure.
    /// It appends the raw query string (including Fields/Pagination) to the base URL.
    /// </summary>
    /// <param name="url">The base API endpoint URL (e.g., "https://api.propertyradar.com/v1/properties").</param>
    /// <param name="bearerToken">The Bearer Token for authorization.</param>
    /// <param name="rawQueryParams">The full query string, including '?' and all parameters (e.g., "?Purchase=1&Fields=...&page=1&pageSize=500").</param>
    /// <returns>The number of records successfully saved to the database.</returns>
    public async Task PostAndSavePropertyRecordsAsync()
    {
        try { 
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

            if (_context.Campaigns.Any())
            {
                var campaigns = _context.Campaigns.ToList();
                Log.Information("Found {Count} campaigns to process.", campaigns.Count);

                foreach (var campaign in campaigns)
                {
                    if (campaign == null || campaign.LeadSource == null)
                        continue;

                    var scheduleType = campaign.LeadSource.Type;
                    var runAt = campaign.LeadSource.RunAt; // TimeSpan (e.g. 00:00:00)
                    var daysOfWeek = campaign.LeadSource.DaysOfWeek; // List<string>
                    string? selectedPrinter = campaign.Printer.ToString();
                    
                    if (scheduleType == ScheduleType.Daily)
                    {
                        var nowTime = DateTime.Now;

                        if (nowTime.TimeOfDay >= runAt)
                        {
                            Log.Information("Running Daily Campaign: {Name}", campaign.Name);
                            // ✅ Run your scheduled code for daily schedule
                            string useApiSimulator = System.Configuration.ConfigurationManager.AppSettings["USeAPISimulator"];
                            if (useApiSimulator?.ToLower() == "true")
                            {
                                // Fetch data from local CSV file
                                var csvPath = GetCsvPathFromDataFolder();

                                var csvData = await ReadPropertiesFromCsv(csvPath, campaign.Id);
                                if (!csvData.Any())
                                {
                                    Log.Warning("no any csvData.");
                                    return;
                                }
                                await SaveCsvPropertiesAsync(campaign, csvData);
                            }
                            else
                            {
                                await RunCampaign(_context, _httpClient, campaign, url, rawQueryParams, bearerToken);
                            }

                            foreach (var stage in campaign.Stages.OrderBy(s => s.DelayDays))
                            {
                                if (!stage.IsRun)
                                {
                                    try
                                    {
                                        if (DateTime.Now.Date >= campaign.ScheduledDate.AddDays(stage.DelayDays).Date)
                                        {
                                            Log.Information("Processing Stage: {StageName} for Campaign: {CampaignName}", stage.StageName, campaign.Name);
                                            var records = await _context.Properties.Where(x => x.CampaignId == campaign.Id && x.IsBlackListed == false).ToListAsync();
                                            var templatePath = await _context.Templates.Where(x => x.Id.ToString() == stage.TemplateId).Select(x => x.Path).FirstOrDefaultAsync();
                                            var outputPath = Path.Combine(campaign.OutputPath, stage.StageName);
                                            
                                            if (!Directory.Exists(outputPath))
                                            {
                                                Directory.CreateDirectory(outputPath);
                                            }
                                            if (templatePath != null)
                                            {
                                                string outputFileName = Path.Combine(outputPath, $"{campaign.Name}.docx");

                                                await _engine.ExportBatch(templatePath, records, Path.Combine(outputPath, $"{campaign.Name}.docx"));

                                                // Convert DOCX to PDF
                                                string pdfFileName = Path.Combine(outputPath, $"{campaign.Name}.pdf");
                                                using (WordDocument wordDocument = new WordDocument(outputFileName, Syncfusion.DocIO.FormatType.Automatic))
                                                {
                                                    var converter = new DocToPDFConverter();
                                                    using (var pdfDocument = converter.ConvertToPDF(wordDocument))
                                                    {
                                                        pdfDocument.Save(pdfFileName);  // ✅ Save PDF to .pdf file
                                                    }
                                                }
                                                foreach (var item in records)
                                                {
                                                    AddRecordToPrintHistory(item.Id,campaign,stage,campaign.Printer, outputFileName);
                                                   
                                                }
                                                // Verify the file was created
                                                if (!File.Exists(outputFileName))
                                                {
                                                    Log.Error($"Failed to generate document: {outputFileName}");
                                                    return;
                                                }
                                            }
                                            stage.IsRun = true;

                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Log.Error(ex, "Error processing stage {StageName} for campaign {CampaignName}", stage.StageName, campaign.Name);
                                        // throw; // Don't crash the loop
                                    }
                                    campaign.LastRunningTime = DateTime.Now;
                                    await _context.SaveChangesAsync();
                                }
                            }
                        }
                    }
                    else if (scheduleType == ScheduleType.None)
                    {
                        // Example: daysOfWeek = ["Monday", "Wednesday", "Friday"]
                        var today = DateTime.Now.DayOfWeek.ToString(); // e.g. "Monday"

                        if (daysOfWeek != null && daysOfWeek.Contains(today, StringComparer.OrdinalIgnoreCase))
                        {
                            if (DateTime.Now.TimeOfDay >= runAt)
                            {
                                Log.Information("Running Scheduled Campaign: {Name} on {Day}", campaign.Name, today);
                                // ✅ Run your scheduled code for specific days
                                string useApiSimulator = System.Configuration.ConfigurationManager.AppSettings["USeAPISimulator"];
                                if (useApiSimulator?.ToLower() == "true")
                                {
                                    var csvPath = GetCsvPathFromDataFolder();

                                    var csvData = await ReadPropertiesFromCsv(csvPath, campaign.Id);
                                    if (!csvData.Any())
                                    {
                                        return;
                                    }


                                    await SaveCsvPropertiesAsync(campaign, csvData);
                                }
                                else
                                {
                                    await RunCampaign(_context, _httpClient, campaign, url, rawQueryParams, bearerToken);
                                }
                                foreach (var stage in campaign.Stages.OrderBy(s => s.DelayDays))
                                {
                                    if (!stage.IsRun)
                                    {
                                        if (DateTime.Now.Date >= campaign.ScheduledDate.AddDays(stage.DelayDays).Date)
                                        {
                                            var records = await _context.Properties.Where(x => x.CampaignId == campaign.Id).ToListAsync();
                                            var templatePath = await _context.Templates.Where(x => x.Id.ToString() == stage.TemplateId).Select(x => x.Path).FirstOrDefaultAsync();
                                            var outputPath = Path.Combine(campaign.OutputPath, stage.StageName);
                                            if (!Directory.Exists(outputPath))
                                            {
                                                Directory.CreateDirectory(outputPath);
                                            }
                                                if (templatePath != null)
                                                {
                                                    string outputFileName = Path.Combine(outputPath, $"{campaign.Name}.docx");

                                                    await _engine.ExportBatch(templatePath, records, Path.Combine(outputPath, $"{campaign.Name}.docx"));

                                                // Convert DOCX to PDF first, then print

                                                // Convert DOCX to PDF
                                                string pdfFileName = Path.Combine(outputPath, $"{campaign.Name}.pdf");
                                                using (WordDocument wordDocument = new WordDocument(outputFileName, Syncfusion.DocIO.FormatType.Automatic))
                                                {
                                                    var converter = new DocToPDFConverter();
                                                    using (var pdfDocument = converter.ConvertToPDF(wordDocument))
                                                    {
                                                        pdfDocument.Save(pdfFileName);
                                                    }
                                                }
                                                foreach (var item in records)
                                                    {
                                                        AddRecordToPrintHistory(item.Id, campaign, stage, campaign.Printer, outputFileName);

                                                    }
                                                // Verify the file was created
                                                     if (!File.Exists(outputFileName))
                                                     {
                                                            Log.Error($"Failed to generate document: {outputFileName}");
                                                            return;
                                                     }


                                                }
                                            stage.IsRun = true;
                                        }
                                    }
                                    campaign.LastRunningTime = DateTime.Now;
                                    await _context.SaveChangesAsync();
                                }
                            }
                        }
                    }
                }
            }
            else
            {
               Log.Information("No campaigns found in database."); 
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in PostAndSavePropertyRecordsAsync");
        }
    }

    private async Task RunCampaign(
    MailMergeDbContext _context,
    HttpClient _httpClient,
    Campaign campaign,
    string url,
    string rawQueryParams,
    string? bearerToken)
    {
        try
        {
            const int batchSize = 500;
            int start = 0;
            int totalResultsFetched = 0;

            bool hasMoreData = true;

            while (hasMoreData)
            {
                // Build paginated URL
                var pagedUrl = $"{url}{rawQueryParams}&Start={start}";
                Log.Debug("Fetching records starting from {Start}", start);

                // Request body (filters)
                var jsonContent = campaign.LeadSource.FiltersJson;
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                // Send POST request
                var response = await _httpClient.PostAsync(pagedUrl, content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Log.Error("API Call Failed at start={Start}. Status: {StatusCode}. Details: {Details}",
                        start, response.StatusCode, errorContent);
                    break;
                }

                // Deserialize response
                var responseStream = await response.Content.ReadAsStreamAsync();
                var apiResponse = await JsonSerializer.DeserializeAsync<ApiResponse>(responseStream);

                if (apiResponse?.Results == null || !apiResponse.Results.Any())
                {
                    Log.Information("No more results returned from API at start={Start}. Ending pagination.", start);
                    break;
                }

                var currentBatchCount = apiResponse.Results.Count;
                totalResultsFetched += currentBatchCount;

                Log.Information("Received {Count} records (total fetched so far: {TotalFetched})",
                    currentBatchCount, totalResultsFetched);

                // Map API DTOs to database entities
                var propertiesToSave = apiResponse.Results
                    .Select(dto => MapToPropertyRecord(dto))
                    .ToList();

                // Assign CampaignId to all (new and existing)
                foreach (var prop in propertiesToSave)
                {
                    prop.CampaignId = campaign.Id;
                }

                // Extract RadarIds for duplicate check
                var radarIds = propertiesToSave.Select(p => p.RadarId).ToList();

                // Fetch only the existing records we might conflict with
                var existingRecords = await _context.Properties
                    .Where(p => radarIds.Contains(p.RadarId))
                    .ToListAsync();

                // Use HashSet for fast lookup
                var existingRadarIdSet = new HashSet<string>(existingRecords.Select(e => e.RadarId));

                // Separate new records
                var newProperties = propertiesToSave
                    .Where(p => !existingRadarIdSet.Contains(p.RadarId))
                    .ToList();

                // Update CampaignId on existing records (in case it changed)
                foreach (var existing in existingRecords)
                {
                    existing.CampaignId = campaign.Id;
                }

                // Add new records if any
                if (newProperties.Any())
                {
                    await _context.Properties.AddRangeAsync(newProperties);
                }

                // Single SaveChanges per batch: saves both new inserts and existing updates
                int savedCount = await _context.SaveChangesAsync();

                Log.Information("Batch processed: {NewCount} new properties inserted/updated in this batch. Total saved changes: {SavedCount}",
                    newProperties.Count, savedCount);

                // Decide whether to continue
                // Continue if the API returned a full batch (likely more data exists)
                // Stop if we got fewer than batchSize (last page)
                hasMoreData = currentBatchCount == batchSize;

                start += batchSize;
            }

            // Update campaign last run timestamp (use UTC for consistency)
            campaign.LastRunningTime = DateTime.UtcNow;

            // Final save for the campaign update (outside loop to avoid unnecessary saves if no batches ran)
            await _context.SaveChangesAsync();

            Log.Information("Campaign '{CampaignName}' completed successfully. Total records processed: {TotalFetched}",
                campaign.Name, totalResultsFetched);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in RunCampaign for Campaign '{CampaignName}'", campaign.Name);
            // Optionally re-throw or handle further (e.g., mark campaign as failed)
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

    private async Task<bool> PrintDocumentViaPdf(string docxPath, string printerName)
    {
        string pdfPath = string.Empty;

        try
        {
            if (!File.Exists(docxPath))
            {
                Log.Error($"File not found: {docxPath}");
                return false;
            }

            // Verify printer exists
            var printers = System.Drawing.Printing.PrinterSettings.InstalledPrinters;
            bool printerExists = false;
            foreach (string printer in printers)
            {
                if (printer.Equals(printerName, StringComparison.OrdinalIgnoreCase))
                {
                    printerExists = true;
                    break;
                }
            }

            if (!printerExists)
            {
                Log.Error($"Printer '{printerName}' not found. Available printers: {string.Join(", ", printers.Cast<string>())}");
                return false;
            }

            // ✅ Step 1: Convert DOCX to PDF
            pdfPath = Path.ChangeExtension(docxPath, ".pdf");

            await Task.Run(() =>
            {
                WordDocument wordDocument = new WordDocument(docxPath, Syncfusion.DocIO.FormatType.Automatic);
                var converter = new DocToPDFConverter();
                var pdfDocument = converter.ConvertToPDF(wordDocument);

                string tempFile = Path.Combine(Path.GetTempPath(), $"mailmerge_preview_{Guid.NewGuid()}.pdf");
                pdfDocument.Save(tempFile);
                pdfDocument.Close(true);

                // Keep track so we can optionally delete later
                lastTempPdfPath = tempFile;
            });

            Log.Information($"PDF created: {pdfPath}");

            // ✅ Step 2: Print PDF using PdfiumViewer
            await Task.Run(() =>
            {
                using (var pdfDoc = PdfiumViewer.PdfDocument.Load(pdfPath))
                using (var printDoc = pdfDoc.CreatePrintDocument())
                {
                    printDoc.DocumentName = $"{docxPath}";
                    printDoc.PrinterSettings.PrinterName = printerName;
                    printDoc.Print();
                }
            });

            Log.Information($"Document sent to printer: {printerName}");

            return true;
        }
        catch (Exception ex)
        {
            Log.Error($"Error printing document '{Path.GetFileName(docxPath)}' to '{printerName}': {ex.Message}");
            return false;
        }
        finally
        {
            // Delete temporary PDF file after printing
                   
            try
            {
                if (!string.IsNullOrEmpty(pdfPath) && File.Exists(pdfPath))
                {
                    File.Delete(pdfPath);
                    Log.Information($"Temporary PDF deleted: {pdfPath}");
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"Could not delete temporary PDF: {ex.Message}");
            }
            
        }
    }

    public async Task<bool> RunSingleCampaignAsync(Campaign campaign)
    {
        try
        {
            string RequestedFields = "RadarID,APN,PType,Address,City,State,ZipFive,Owner,OwnershipType,PrimaryName,PrimaryFirstName,OwnerAddress,OwnerCity,OwnerZipFive,OwnerState,inForeclosure,ForeclosureStage,ForeclosureDocType,ForeclosureRecDate,isSameMailing,Trustee,TrusteePhone,TrusteeSaleNum";
            string url = "https://api.propertyradar.com/v1/properties";
            string? bearerToken = System.Configuration.ConfigurationManager.AppSettings["API Key"];

            if (string.IsNullOrEmpty(bearerToken))
            {
                Log.Warning("API Key missing in configuration.");
                return false;
            }

            string rawQueryParams = $"?Purchase=1&Fields={RequestedFields}";

            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

            Log.Information("Manually running Campaign: {Name}", campaign.Name);

            // ✅ Step 1: Fetch data from PropertyRadar API
            await RunCampaign(_context, httpClient, campaign, url, rawQueryParams, bearerToken);

            // ✅ Step 2: Process all stages for this campaign
            foreach (var stage in campaign.Stages)
            {
                try
                {
                    Log.Information("Processing Stage: {StageName} for Campaign: {CampaignName}",
                        stage.StageName, campaign.Name);

                    var records = await _context.Properties
                        .Where(x => x.CampaignId == campaign.Id && x.IsBlackListed == false)
                        .ToListAsync();

                    if (!records.Any())
                    {
                        Log.Warning("No records found for campaign {CampaignName}, stage {StageName}",
                            campaign.Name, stage.StageName);
                        continue;
                    }

                    var templatePath = await _context.Templates
                        .Where(x => x.Id.ToString() == stage.TemplateId)
                        .Select(x => x.Path)
                        .FirstOrDefaultAsync();

                    if (string.IsNullOrEmpty(templatePath) || !File.Exists(templatePath))
                    {
                        Log.Error("Template not found for stage {StageName}", stage.StageName);
                        continue;
                    }

                    var outputPath = Path.Combine(campaign.OutputPath, stage.StageName);

                    if (!Directory.Exists(outputPath))
                    {
                        Directory.CreateDirectory(outputPath);
                    }

                    string outputFileName = Path.Combine(outputPath, $"{campaign.Name}.docx");

                    // Generate mail merge document
                    await _engine.ExportBatch(templatePath, records, outputFileName);

                    // Verify the file was created
                    if (!File.Exists(outputFileName))
                    {
                        Log.Error($"Failed to generate document: {outputFileName}");
                        continue;
                    }

                    // Convert DOCX to PDF
                    string pdfFileName = Path.Combine(outputPath, $"{campaign.Name}.pdf");
                    using (WordDocument wordDocument = new WordDocument(outputFileName, Syncfusion.DocIO.FormatType.Automatic))
                    {
                        var converter = new DocToPDFConverter();
                        using (var pdfDocument = converter.ConvertToPDF(wordDocument))
                        {
                            pdfDocument.Save(pdfFileName);
                        }
                    }

                    // Add records to print history
                    foreach (var item in records)
                    {
                        await AddRecordToPrintHistory(item.Id, campaign, stage, campaign.Printer, outputFileName);
                    }

                    Log.Information("Successfully processed stage {StageName} for campaign {CampaignName}",
                        stage.StageName, campaign.Name);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error processing stage {StageName} for campaign {CampaignName}",
                        stage.StageName, campaign.Name);
                    // Continue with next stage
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in RunSingleCampaignAsync for campaign {CampaignName}", campaign.Name);
            return false;
        }
    }
    private async Task AddRecordToPrintHistory(int propertyId, Campaign campaign, FollowUpStage stage, string selectedPrinter, string pdfPath)
    {
        _context.PrintHistory.Add(new PrintHistory
        {
            PropertyId = propertyId,
            CampaignId = campaign.Id,
            StageId = stage.Id,
            PrinterName = selectedPrinter,
            FilePath = pdfPath
        });
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Fetches property data from PropertyRadar API for a given campaign without saving to database.
    /// Returns the campaign object with updated TotalRecordsFetched count.
    /// </summary>
    /// <summary>
    /// Gets the count of properties available from PropertyRadar API for the campaign
    /// </summary>
    public async Task<int> GetCampaignPropertyCountFromApiAsync(Campaign campaign)
    {
        try
        {
            // Hardcoded configuration
            string RequestedFields = "RadarID,APN,PType,Address,City,State,ZipFive,Owner,OwnershipType,PrimaryName,PrimaryFirstName,OwnerAddress,OwnerCity,OwnerZipFive,OwnerState,inForeclosure,ForeclosureStage,ForeclosureDocType,ForeclosureRecDate,isSameMailing,Trustee,TrusteePhone,TrusteeSaleNum";
            string url = "https://api.propertyradar.com/v1/properties";
            string? bearerToken = System.Configuration.ConfigurationManager.AppSettings["API Key"];

            if (string.IsNullOrEmpty(bearerToken))
            {
                Log.Warning("API Key missing in configuration.");
                return 0;
            }

            string rawQueryParams = $"?Purchase=1&Fields={RequestedFields}";

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

            // We only need the first request to get total count
            var pagedUrl = $"{url}{rawQueryParams}&Start=0";

            var jsonContent = campaign.LeadSource.FiltersJson;
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(pagedUrl, content);
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Log.Error("API Call Failed. Status: {StatusCode}. Details: {Details}",
                    response.StatusCode, errorContent);
                return 0;
            }

            var responseStream = await response.Content.ReadAsStreamAsync();
            var apiResponse = await JsonSerializer.DeserializeAsync<ApiResponse>(responseStream);

            if (apiResponse?.Results == null)
            {
                Log.Information("No results found for campaign {CampaignName}", campaign.Name);
                return 0;
            }

            Log.Information("Total {Count} properties available from API for campaign {CampaignName}",
                apiResponse.TotalResultCount, campaign.Name);

            return apiResponse.TotalResultCount;
        }
        catch (Exception ex)
        {
            Log.Error($"Error in GetCampaignPropertyCountFromApiAsync for Campaign {campaign.Name}: {ex.Message}");
            return 0;
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

            var existing = await _context.Properties
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
                await _context.Properties.AddRangeAsync(newRecords);
            }

            await _context.SaveChangesAsync();

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