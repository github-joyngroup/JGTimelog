using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text;
using Timelog.Common.Models;
using Timelog.Common.TCP;
using WatsonTcp;

namespace Timelog.Viewer
{
    public class ViewerClient : BackgroundService
    {
        private static ILogger _logger;
        private static ViewerClientConfiguration _configuration;
        private static TCPClientWrapper _client;

        /// <summary>
        /// Starts the TimeLogReportingServerDriver based on the configuration. Will setup the Server host and port, and wire up the several TCP events
        /// </summary>
        public static void Startup(ViewerClientConfiguration configuration, ILogger logger)
        {
            _configuration = configuration;
            _logger = logger;

            _client = new TCPClientWrapper();
            _client.Startup(_configuration.WatsonTCPClientConfiguration, logger, OnTimelogTCPOperation);

            _logger?.LogInformation($"Timelog.Viewer setup.");
        }

        /// <summary>
        /// Handles a TCP operation. As the TCP communication is handled by the TCPClientWrapper this class shall only handle business logic
        /// </summary>
        private static void OnTimelogTCPOperation(TimelogTCPOperation operation, Guid clientGuid, List<FilterCriteria> filters)
        {
            switch(operation)
            {
                case TimelogTCPOperation.CurrentFilter:
                    _logger?.LogInformation("My current filter is: " + System.Text.Json.JsonSerializer.Serialize(filters.First()));
                    break;

                default:
                    _logger?.LogDebug($"Operation not implemented: {operation.ToString()}");
                    break;
            }
        }

        /// <summary>
        /// Starts the TimeLogReportingServerDriver
        /// Will connect to the Timelog Reporting server and keep the connection alive
        /// When the connection is lost, it will retry to connect
        /// When the connection is established, it will send a Ping message to the server every CheckConnectionHealthFrequency, this will prevent server to disconnect the client
        /// </summary>
        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger?.LogInformation($"Timelog.Viewer is starting...");
            return Task.Run(() => _client.Start(stoppingToken), stoppingToken);
        }

        /// <summary>
        /// Stops the TimeLogReportingServerDriver and disconnects from the Timelog Reporting server
        /// </summary>
        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger?.LogInformation($"Timelog.Viewer is stopping...");
            await Task.Run(() => _client.Stop(), cancellationToken);
        }

        /// <summary>
        /// Send message to the Timelog Reporting server to set a filter
        /// </summary>
        public static void SetFilter(FilterCriteria filterCriteria)
        {
            _client.SetFilter(new List<FilterCriteria>() { filterCriteria });
        }

        /// <summary>
        /// Send message to the Timelog Reporting server to get my current filter
        /// </summary>
        public static void GetFilter()
        {
            _client.GetFilter();
        }
    }

    public class ViewerClientConfiguration
    {
        public WatsonTcpClientConfiguration WatsonTCPClientConfiguration { get; set; }
    }
}
