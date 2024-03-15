using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Timelog.Common.Models;
using Timelog.Common.TCP;
using WatsonTcp;

namespace Timelog.Reporting
{
    internal class LogServerClient : BackgroundService
    {
        /// <summary>The unique application key to be used in the TCP communication</summary>
        private static Guid _applicationKey { get; set; }

        private static ILogger _logger;
        internal static LogServerClientConfiguration _configuration;
        private static TCPClientWrapper _client;

        public static void Startup(Guid applicationKey, LogServerClientConfiguration configuration, ILogger logger)
        {
            _applicationKey = applicationKey;
            _configuration = configuration;
            _logger = logger;

            _client = new TCPClientWrapper();
            _client.Startup(_applicationKey, _configuration.WatsonTCPClientConfiguration, logger, OnTimelogTCPOperation);

            //Wire up viewer filters changed
            ViewerFiltersHandler.OnViewerFiltersChanged += ViewerFiltersHandler_OnViewerFiltersChanged;

            _logger?.LogInformation($"Timelog.Reporting LogServerClient setup.");
        }

        /// <summary>
        /// Starts the TimeLogReporting LogServer Client
        /// Will connect to the Timelog Server
        /// </summary>
        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger?.LogInformation($"Timelog.Reporting LogServerClient is starting...");
            return Task.Run(() => _client.Start(stoppingToken), stoppingToken);
        }

        /// <summary>
        /// Stops the TimeLogReporting LogServer and disconnects from the Timelog server
        /// </summary>
        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger?.LogInformation($"Timelog.Reporting LogServerClient is stopping...");
            await Task.Run(() => _client.Stop(), cancellationToken);
        }

        /// <summary>
        /// Handles a TCP operation. As the TCP communication is handled by the TCPClientWrapper this class shall only handle business logic
        /// </summary>
        private static void OnTimelogTCPOperation(TimelogTCPOperation operation, Guid clientGuid, List<FilterCriteria> filters)
        {
            switch (operation)
            {
                default:
                    _logger?.LogDebug($"Operation not implemented: {operation.ToString()}");
                    break;
            }
        }

        /// <summary>
        /// Triggered when the viewer filters are changed - shall send them to the Log Server so he can filter the logs accordingly
        /// </summary>
        /// <param name="currentFilters"></param>
        /// <exception cref="NotImplementedException"></exception>
        private static void ViewerFiltersHandler_OnViewerFiltersChanged(Dictionary<Guid, FilterCriteria> currentFilters)
        {
            _client.SetFilter(currentFilters.Values.ToList());
        }
    }

    internal class LogServerClientConfiguration
    {
        public WatsonTcpClientConfiguration WatsonTCPClientConfiguration { get; set; }
    }
}
