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
                            await RunCampaign(_context,_httpClient,campaign,url,rawQueryParams,bearerToken);
                            
                            foreach (var stage in campaign.Stages)
                            {
                                if (!stage.IsRun)
                                {
                                    try
                                    {
                                        if (DateTime.Now >= campaign.LastRunningTime.AddDays(stage.DelayDays))
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
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Log.Error(ex, "Error processing stage {StageName} for campaign {CampaignName}", stage.StageName, campaign.Name);
                                        // throw; // Don't crash the loop
                                    }
                                
                                    stage.IsRun = true;
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
                                await RunCampaign(_context,_httpClient,campaign, url, rawQueryParams, bearerToken);
                                foreach (var stage in campaign.Stages)
                                {
                                    if (!stage.IsRun)
                                    {
                                        if (DateTime.Now >= campaign.LastRunningTime.AddDays(stage.DelayDays))
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
                                            }

                                        stage.IsRun = true;
                                    }
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

    private async Task RunCampaign(MailMergeDbContext _context, HttpClient _httpClient, Campaign campaign, string url, string rawQueryParams, string? bearerToken)
    {
        try
        {
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
                var jsonContent = campaign.LeadSource.FiltersJson;
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

                propertiesToSave.ForEach(x => x.CampaignId = campaign.Id);

                var radarIds = propertiesToSave.Select(p => p.RadarId).ToList();

                // Fetch existing records with matching RadarIds
                var existingRecords = await _context.Properties
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
                    existing.CampaignId = campaign.Id;
                }

                // ✅ Add new records
                if (newProperties.Any())
                {
                    await _context.Properties.AddRangeAsync(newProperties);
                    int saved = await _context.SaveChangesAsync();
                    Log.Information("Saved {Count} new properties.", saved);
                }
                else
                {
                    Log.Debug("No new records to insert for batch starting {Start}.", start);
                }
                await _context.SaveChangesAsync();
                
                start += batchSize;
                moreData = start < totalResults;

            } while (moreData);

            campaign.LastRunningTime = DateTime.Now;
            await _context.SaveChangesAsync();

        }
        catch (Exception ex)
        {
            Log.Error($"Error in RunCampaign for Campaign {campaign.Name}: {ex.Message}");
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

}