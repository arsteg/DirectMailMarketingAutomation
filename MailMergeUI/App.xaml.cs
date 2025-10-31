using MailMerge.Data;
using MailMergeUI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PdfSharp.Fonts;
using System;
using System.IO;
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
                })
                .Build();

            // Capture the service provider for global access
            Services = _appHost.Services;
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
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
