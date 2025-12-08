using MailMerge.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Syncfusion.Licensing;


try
{
    var builder = Host.CreateApplicationBuilder(args);

    // Read Syncfusion license
    string? licenseKey = System.Configuration.ConfigurationManager.AppSettings["SyncfusionLicense"];

    if (!string.IsNullOrWhiteSpace(licenseKey))
    {
        SyncfusionLicenseProvider.RegisterLicense(licenseKey);
    }
    else
    {
        Log.Warning("Syncfusion License Key not found in appsettings.json");
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

    var host = builder.Build();

    // create a scope so that scoped services like DbContext work correctly
    using (var scope = host.Services.CreateScope())
    {
        try 
        {
            var api = scope.ServiceProvider.GetRequiredService<ApiService>();
            // call your method
            await api.PostAndSavePropertyRecordsAsync();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Fatal error during startup execution scope");
        }
    }

    Log.Information("Starting Windows Service Host");
    host.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Windows Service Host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
