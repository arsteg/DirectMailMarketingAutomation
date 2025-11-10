using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp1
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly MailMergeEngine.MailMergeEngine _engine;

        public Worker(ILogger<Worker> logger, MailMergeEngine.MailMergeEngine engine)
        {
            _logger = logger;
            _engine = engine;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                }
                var service = new ApiService(_engine);
                await service.PostAndSavePropertyRecordsAsync();
                await Task.Delay(1000, stoppingToken);
            }
        }
    }
}
