using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Timelog.Common.Models;
using WatsonTcp;

namespace Timelog.Reporting
{
    internal class ReportingServer : BackgroundService
    {
        private static ILogger _logger;
        internal static ReportingServerConfiguration Configuration;

        public static void Startup(ReportingServerConfiguration configuration, ILogger logger)
        {
            Configuration = configuration;
            _logger = logger;

            _logger?.LogInformation($"Timelog.ReportingServer setup.");
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger?.LogInformation($"Timelog.ReportingServer is starting...");
            return Task.Run(() => "0".ToString(), stoppingToken);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger?.LogInformation($"Timelog.ReportingServer is stopping...");
            await Task.Run(() => "0".ToString(), cancellationToken);
        }
    }

    internal class ReportingServerConfiguration
    {
        /// <summary>
        /// The Timelog Reporting Unique AppKey
        /// </summary>
        public Guid AppKey { get; set; }
    }
}
