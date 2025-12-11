using MailMerge.Data;
using MailMergeUI.Logging;
using MailMergeUI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting;
using PdfSharp.Fonts;
using Serilog;
using Syncfusion.Licensing;
using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows;

namespace MailMergeUI
{
    public partial class App : Application
    {
        // Expose IServiceProvider globally (anti-pattern way)
        public static IServiceProvider? Services { get; private set; }
        private static IHost? _appHost;

        public App()
        {
            string exePath = Assembly.GetExecutingAssembly().Location;
            string exeFolder = Path.GetDirectoryName(exePath);
            string logsFolderPath = Path.Combine(exeFolder, "Logs");

            if (!Directory.Exists(logsFolderPath))
            {
                Directory.CreateDirectory(logsFolderPath);
            }

            _appHost = Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration((hostContext, config) =>
                {
                    config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                    config.AddEnvironmentVariables();
                })
                .ConfigureServices((hostContext, services) =>
                {
                    string dbPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "MailMax",
                        "mailmerge.db");

                    Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
                    string connectionString = $"Data Source={dbPath}";

                    services.AddDbContext<MailMergeDbContext>(options =>
                    {
                        options.UseSqlite(connectionString);
                    });

                    // Register services and windows
                    services.AddScoped<MailMergeEngine.MailMergeEngine>();
                    services.AddScoped<CampaignService>();
                    services.AddTransient<LoginWindow>();
                    services.AddTransient<DashboardWindow>();
                    services.AddTransient<SettingsWindow>();
                    services.AddTransient<MailMergeWindow>();
                    services.AddTransient<TemplateWindow>();
                    services.AddTransient<DashboardService>();
                })
                .Build();

            // Capture the service provider for global access
            Services = _appHost.Services;

            // Register Syncfusion license from appsettings
            string? licenseKey = _appHost.Services.GetRequiredService<IConfiguration>()["SyncfusionLicense"];

            if (!string.IsNullOrWhiteSpace(licenseKey))
            {
                SyncfusionLicenseProvider.RegisterLicense(licenseKey);
            }
            else
            {
                MessageBox.Show("Syncfusion license key not found in appsettings.json!",
                                "License Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            // Configure logger first
            Log.Logger = LogHelper.Configure();
            Log.Information("=== Application started ===");

            // Global exception handling
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                Log.Fatal(ex, "AppDomain Unhandled exception");
                MessageBox.Show($"Fatality: {ex?.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            };

            DispatcherUnhandledException += (s, args) =>
            {
                Log.Fatal(args.Exception, "Dispatcher Unhandled exception");
                MessageBox.Show($"An unexpected error occurred: {args.Exception.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true; // Prevent crash if possible
            };

            await _appHost!.StartAsync();

            ThemeManager.InitializeTheme();
            GlobalFontSettings.UseWindowsFontsUnderWindows = true;

            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Anti-pattern: resolve window from global App.Services
            var startupForm = Services!.GetRequiredService<LoginWindow>();
            startupForm.Show();

            base.OnStartup(e);
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            await _appHost!.StopAsync();
            base.OnExit(e);
        }
    }
}
