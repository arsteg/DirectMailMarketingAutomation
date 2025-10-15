using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

public class ApiService
{
    private readonly ApplicationDbContext _context;
    private readonly HttpClient _httpClient;

    public ApiService(ApplicationDbContext context, HttpClient httpClient)
    {
        _context = context;
        _httpClient = httpClient;
    }

    /// <summary>
    /// Performs a POST API call using the specific PropertyRadar criteria structure.
    /// It appends the raw query string (including Fields/Pagination) to the base URL.
    /// </summary>
    /// <param name="url">The base API endpoint URL (e.g., "https://api.propertyradar.com/v1/properties").</param>
    /// <param name="bearerToken">The Bearer Token for authorization.</param>
    /// <param name="rawQueryParams">The full query string, including '?' and all parameters (e.g., "?Purchase=1&Fields=...&page=1&pageSize=500").</param>
    /// <param name="searchCriteriaBody">The object structured as { "Criteria": [ ... ] } for the request body.</param>
    /// <returns>The number of records successfully saved to the database.</returns>
    public async Task<int> PostAndSavePropertyRecordsAsync(
    string url,
    string bearerToken,
    string rawQueryParams,
    object searchCriteriaBody)
    {
        int totalRecordsSaved = 0;
        int start = 0;
        int batchSize = 500;
        int totalResults = 0;
        bool moreData = true;

        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", bearerToken);

        do
        {
            // 1️⃣ Build URL with pagination
            var pagedUrl = $"{url}{rawQueryParams}&Start={start}";
            Console.WriteLine($"\nFetching records starting from {start}...");

            // 2️⃣ Serialize request body
            var jsonContent = JsonSerializer.Serialize(searchCriteriaBody);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            // 3️⃣ Send POST
            var response = await _httpClient.PostAsync(pagedUrl, content);
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"\n--- API Call Failed at start={start}. Status: {response.StatusCode} ---\nDetails: {errorContent}");
                break;
            }

            // 4️⃣ Deserialize response
            var responseStream = await response.Content.ReadAsStreamAsync();
            var apiResponse = await JsonSerializer.DeserializeAsync<ApiResponse>(responseStream);

            if (apiResponse?.Results == null || !apiResponse.Results.Any())
            {
                Console.WriteLine("\nNo results found for this batch.");
                break;
            }

            totalResults = apiResponse.TotalResultCount;

            // 5️⃣ Map results
            var propertiesToSave = apiResponse.Results
                .Select(dto => MapToPropertyRecord(dto))
                .ToList();

            // 6️⃣ Remove duplicates before saving
            var radarIds = propertiesToSave.Select(p => p.RadarId).ToList();
            var existingRadarIds = await _context.Properties
                .Where(p => radarIds.Contains(p.RadarId))
                .Select(p => p.RadarId)
                .ToListAsync();

            var newProperties = propertiesToSave
                .Where(p => !existingRadarIds.Contains(p.RadarId))
                .ToList();

            if (newProperties.Any())
            {
                await _context.Properties.AddRangeAsync(newProperties);
                int saved = await _context.SaveChangesAsync();
                totalRecordsSaved += saved;
                Console.WriteLine($"Saved {saved} new records (start={start}).");
            }
            else
            {
                Console.WriteLine($"No new records to insert for batch starting {start}.");
            }

            // 7️⃣ Check if more results remain
            start += batchSize;
            moreData = start < totalResults;

        } while (moreData);

        Console.WriteLine($"\nCompleted fetching all records. Total saved: {totalRecordsSaved} / {totalResults}.");
        return totalRecordsSaved;
    }



    /// <summary>
    /// Maps the DTO properties to the Entity Model properties.
    /// </summary>
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
}