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


using CliWrap;
using ConsoleApp1;
using MailMerge.Data;
using MailMerge.WindowsService.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Serilog;
using Syncfusion.Licensing;
using System.Configuration;

const string ServiceName = "Cache Background Service";

if (args is { Length: 1 })
{
    try
    {
        string executablePath =
            Path.Combine(AppContext.BaseDirectory, "ConsoleApp1.exe");

        if (args[0] is "/Install")
        {
            await Cli.Wrap("sc")
                .WithArguments(new[] { "create", ServiceName, $"binPath={executablePath}", "start=auto" })
                .ExecuteAsync();
        }
        else if (args[0] is "/Uninstall")
        {
            await Cli.Wrap("sc")
                .WithArguments(new[] { "stop", ServiceName })
                .ExecuteAsync();

            await Cli.Wrap("sc")
                .WithArguments(new[] { "delete", ServiceName })
                .ExecuteAsync();
        }
        // Configure once at startup
        Log.Logger = LogHelper.Configure();

        Log.Information("=== Application started ===");

        // Optional: Global exception handling
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            Log.Fatal(args.ExceptionObject as Exception, "Unhandled exception");
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex);
        Log.Fatal(ex, "Failed to install/uninstall the service.");
    }

    return;
}


var builder = Host.CreateApplicationBuilder(args);

// Read Syncfusion license
string? licenseKey = System.Configuration.ConfigurationManager.AppSettings["SyncfusionLicense"];

if (!string.IsNullOrWhiteSpace(licenseKey))
{
    SyncfusionLicenseProvider.RegisterLicense(licenseKey);
}
else
{
    Console.WriteLine("⚠ Syncfusion License Key not found in appsettings.json");
}
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "Cache Background Service";
});
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
builder.Services.AddSingleton<MailMergeEngine.MailMergeEngine>();
builder.Services.AddSingleton<ApiService>();
builder.Services.AddHostedService<WindowsBackgroundService>();
var host = builder.Build();
host.Run();
