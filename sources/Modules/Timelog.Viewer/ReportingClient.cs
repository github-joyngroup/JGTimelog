using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text;
using Timelog.Common.Models;
using Timelog.Common.TCP;
using WatsonTcp;

namespace Timelog.Viewer
{
    public class ReportingClient : BackgroundService
    {
        /// <summary>The unique application key to be used in the TCP communication</summary>
        private static Guid _applicationKey { get; set; }

        private static ILogger _logger;
        private static ReportingClientConfiguration _configuration;
        private static TCPClientWrapper _client;

        public static event OnLogMessagesReceivedHandler OnLogMessagesReceived;
        /// <summary>
        /// Starts the TimeLogReportingServerDriver based on the configuration. Will setup the Server host and port, and wire up the several TCP events
        /// </summary>
        public static void Startup(Guid applicationKey, ReportingClientConfiguration configuration, ILogger logger, OnLogMessagesReceivedHandler onLogMessagesReceived)
        {
            _applicationKey = applicationKey;
            _configuration = configuration;
            _logger = logger;

            OnLogMessagesReceived += onLogMessagesReceived;

            _client = new TCPClientWrapper();
            _client.Startup(_applicationKey, _configuration.WatsonTCPClientConfiguration, logger, OnTimelogTCPOperation);

            _logger?.LogInformation($"Timelog.Viewer setup.");
        }

        /// <summary>
        /// Starts the TimeLogReportingServerDriver
        /// Will connect to the Timelog Reporting server
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
        /// Handles a TCP operation. As the TCP communication is handled by the TCPClientWrapper this class shall only handle business logic
        /// </summary>
        private static void OnTimelogTCPOperation(TimelogTCPOperation operation, Guid clientGuid, List<FilterCriteria> filters, List<LogMessage> logMessages)
        {
            switch (operation)
            {
                case TimelogTCPOperation.CurrentFilter:
                    _logger?.LogInformation("My current filter is: " + System.Text.Json.JsonSerializer.Serialize(filters.First()));
                    break;

                case TimelogTCPOperation.LogMessages:
                    _logger?.LogDebug($"Received {logMessages.Count} from the Reporting Server");
                    OnLogMessagesReceived?.Invoke(logMessages);
                    break;

                default:
                    _logger?.LogDebug($"Operation not implemented: {operation.ToString()}");
                    break;
            }
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

    public class ReportingClientConfiguration
    {
        public WatsonTcpClientConfiguration WatsonTCPClientConfiguration { get; set; }
    }
}
