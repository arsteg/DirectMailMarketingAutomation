//using Microsoft.Extensions.Configuration;
//using System.Configuration;
//public class Program
//{
//    // The specific fields requested in the URL
//    private const string RequestedFields = "RadarID,APN,PType,Address,City,State,ZipFive,Owner,OwnershipType,PrimaryName,PrimaryFirstName,OwnerAddress,OwnerCity,OwnerZipFive,OwnerState,inForeclosure,ForeclosureStage,ForeclosureDocType,ForeclosureRecDate,isSameMailing,Trustee,TrusteePhone,TrusteeSaleNum";

//    public static async Task Main(string[] args)
//    {
//        Console.WriteLine("--- Starting Property Data Sync ---");

//        // --- Configuration (Replace with your actual values) ---
//        const string ApiBaseUrl = "https://api.propertyradar.com/v1/properties";
//        string? BearerToken = ConfigurationManager.AppSettings["API Key"];

//        // Query string parameters (including pagination)
//        string fullQueryParams = $"?Purchase=1&Fields={RequestedFields}";



//        // 3. Run the API call and data save operation
//        try
//        {
//            await apiService.PostAndSavePropertyRecordsAsync(
//                ApiBaseUrl,
//                BearerToken,
//                fullQueryParams // Pass the entire query string
//            );

//            //Console.WriteLine($"\n--- Job Finished. Total Records Saved: {savedCount} ---");
//        }
//        catch (Exception apiEx)
//        {
//            Console.WriteLine($"\n❌ An unexpected error occurred: {apiEx.Message}");
//        }
//    }
//}

using ConsoleApp1;
using MailMerge.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();
string dbPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "MailMax",
                        "mailmerge.db");

Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
string connectionString = $"Data Source={dbPath}";

builder.Services.AddDbContext<MailMergeDbContext>(options =>
{
    options.UseSqlite(connectionString);
});
builder.Services.AddScoped<MailMergeEngine.MailMergeEngine>();

var host = builder.Build();
host.Run();
