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
    internal class ViewerServer : BackgroundService
    {
        private static ILogger _logger;
        internal static ViewerServerConfiguration _configuration;
        internal static TCPServerWrapper _server;

        /// <summary>
        /// Starts the ViewerServer based on the configuration. Will setup the Server host and port, and wire up the several TCP events
        /// </summary>
        /// <param name="configuration"></param>
        /// <param name="logger"></param>
        public static void Startup(ViewerServerConfiguration configuration, ILogger logger)
        {
            _configuration = configuration;
            _logger = logger;

            _server = new TCPServerWrapper();
            _server.Startup(_configuration.WatsonTcpServerConfiguration, logger, OnTimelogTCPOperation);

            _logger?.LogInformation($"Timelog.Reporting.ViewerServer setup.");
        }

        /// <summary>
        /// Starts the ViewerServer
        /// </summary>
        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger?.LogInformation($"Timelog.Reporting.ViewerServer is starting...");
            return Task.Run(() => _server.Start(), stoppingToken);
        }

        /// <summary>
        /// Stops the ViewerServer
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger?.LogInformation($"Timelog.Reporting.ViewerServer is stopping...");
            await Task.Run(() => _server.Stop(), cancellationToken);
        }

        /// <summary>
        /// Handles a TCP operation. As the TCP communication is handled by the TCPServerWrapper this class shall only handle business logic
        /// </summary>
        private static void OnTimelogTCPOperation(TimelogTCPOperation operation, Guid clientGuid, List<FilterCriteria> filters, List<LogMessage> logMessages)
        {
            switch (operation)
            {

                case TimelogTCPOperation.Disconnect:
                    ViewerFiltersHandler.RemoveFilter(clientGuid);
                    break;

                case TimelogTCPOperation.SetFilter:
                    if (filters.Any())
                    {
                        ViewerFiltersHandler.AddFilters(clientGuid, filters);
                    }
                    break;

                case TimelogTCPOperation.GetFilter:
                    var retFilters = ViewerFiltersHandler.GetFilters(clientGuid);
                    _server.SendCurrentFilter(clientGuid, retFilters);
                    break;

                default:
                    _logger?.LogDebug($"Operation not implemented: {operation.ToString()} - Client: '{clientGuid.ToString()}'");
                    break;
            }
        }

        /// <summary>
        /// Sends a string message to every connected client
        /// </summary>
        public static void BroadcastMessage(string message)
        {
            _server.BroadcastMessage(message);
        }

        /// <summary>
        /// sends a list of log messages to a specific client
        /// </summary>
        public static void SendLogMessages(Guid viewerGuid, List<LogMessage> logMessages)
        {
            _server.SendLogMessages(viewerGuid, logMessages);
        }

        public static List<string> ListClients()
        {
            return _server.ListClients().Select(c => c.ToString()).ToList();
        }
    }

    public class ViewerServerConfiguration
    {
        public WatsonTcpServerConfiguration WatsonTcpServerConfiguration { get; set; }
    }
}
