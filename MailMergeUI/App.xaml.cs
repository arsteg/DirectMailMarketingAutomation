using MailMerge.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PdfSharp.Fonts;
using System.Windows;

namespace MailMergeUI
{
    public partial class App : Application
    {
        // 1. Add a static property to hold the host
        public static IHost? AppHost { get; private set; }

        public App()
        {
            // 2. Configure and build the host in the constructor
            AppHost = Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration((hostContext, config) =>
                {
                    // Add appsettings.json to the configuration
                    config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                    config.AddEnvironmentVariables();
                })
                .ConfigureServices((hostContext, services) =>
                {
                    // Get the configuration from appsettings.json
                    IConfiguration configuration = hostContext.Configuration;
                    string? connectionString = configuration.GetConnectionString("DefaultConnection");

                    // Register your DbContext
                    services.AddDbContext<MailMergeDbContext>(options =>
                    {
                        options.UseSqlServer(connectionString);
                    });                   

                    // TODO: Register any other ViewModels or Services here
                    services.AddScoped<MailMergeEngine.MailMergeEngine>();
                    services.AddScoped<LoginWindow>();
                    services.AddScoped<DashboardWindow>();
                    services.AddScoped<SettingsWindow>();
                    services.AddScoped<MailMergeWindow>();
                    services.AddScoped<TemplateWindow>();

                })
                .Build();
        }

        // 3. Modify OnStartup to be async
        protected override async void OnStartup(StartupEventArgs e)
        {
            // Start the host
            await AppHost!.StartAsync();

            // --- Your existing startup logic can run here ---
            ThemeManager.InitializeTheme();
            GlobalFontSettings.UseWindowsFontsUnderWindows = true;
            // ---

            // Get the main window from the DI container and show it
            var startupForm = AppHost.Services.GetRequiredService<LoginWindow>();
            startupForm.Show();

            base.OnStartup(e);
        }

        // 4. (Recommended) Add OnExit to stop the host gracefully
        protected override async void OnExit(ExitEventArgs e)
        {
            await AppHost!.StopAsync();
            base.OnExit(e);
        }
    }
}