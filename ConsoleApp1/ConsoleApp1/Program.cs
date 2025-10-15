using Microsoft.Extensions.Configuration;
using System.Configuration;
public class Program
{
    // The specific fields requested in the URL
    private const string RequestedFields = "RadarID,APN,PType,Address,City,State,ZipFive,Owner,OwnershipType,PrimaryName,PrimaryFirstName,OwnerAddress,OwnerCity,OwnerZipFive,OwnerState,inForeclosure,ForeclosureStage,ForeclosureDocType,ForeclosureRecDate,isSameMailing,Trustee,TrusteePhone,TrusteeSaleNum";

    public static async Task Main(string[] args)
    {
        Console.WriteLine("--- Starting Property Data Sync ---");

        // --- Configuration (Replace with your actual values) ---
        const string ApiBaseUrl = "https://api.propertyradar.com/v1/properties";
        string? BearerToken = ConfigurationManager.AppSettings["API Key"];

        // Query string parameters (including pagination)
        string fullQueryParams = $"?Purchase=1&Fields={RequestedFields}";

        string? city = ConfigurationManager.AppSettings["City"];

        // JSON Request Body (Criteria structure)
        var searchCriteriaBody = new
        {
            Criteria = new[]
            {
                new
                {
                    name = "City",
                    value = new[] { city?? "Los Angeles" }
                },
                new
                {
                    name= "inForeclosure",
                    value = new[] { "1" }
                },
                new
                {
                    name= "ForeclosureStage",
                    value = new[] { "Preforeclosure", "Auction" }
                },
                new
                {
                    name= "ForeclosureRecDate",
                    value = new[] { "Last Week" }
                }
                // You can add more criteria items here for filtering (e.g., City, County)
            }
        };
        // --------------------------------------------------------

        // ... (Database and HttpClient initialization remains the same)
        using var context = new ApplicationDbContext();
        await context.Database.EnsureCreatedAsync();

        using var httpClient = new HttpClient();
        var apiService = new ApiService(context, httpClient);

        // 3. Run the API call and data save operation
        try
        {
            int savedCount = await apiService.PostAndSavePropertyRecordsAsync(
                ApiBaseUrl,
                BearerToken,
                fullQueryParams, // Pass the entire query string
                searchCriteriaBody // Pass the structured criteria body
            );

            Console.WriteLine($"\n--- Job Finished. Total Records Saved: {savedCount} ---");
        }
        catch (Exception apiEx)
        {
            Console.WriteLine($"\n❌ An unexpected error occurred: {apiEx.Message}");
        }
    }
}