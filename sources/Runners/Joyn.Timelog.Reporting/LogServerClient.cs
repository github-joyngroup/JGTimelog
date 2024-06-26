﻿using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Joyn.Timelog.Common;
using Joyn.Timelog.Common.Models;
using Joyn.Timelog.Common.TCP;
using WatsonTcp;

namespace Joyn.Timelog.Reporting
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
        private static void OnTimelogTCPOperation(TimelogTCPOperation operation, Guid clientGuid, List<FilterCriteria> filters, List<LogMessage> logMessages)
        {
            switch (operation)
            {
                case TimelogTCPOperation.Connect:
                    _logger?.LogInformation($"Connected to the Timelog Server with Guid: {clientGuid}");
                    //Send current filters to the server
                    _client.SetFilter(ViewerFiltersHandler.ListFilters());
                    break;

                case TimelogTCPOperation.LogMessages:
                    _logger?.LogInformation($"Received {logMessages.Count} from the Timelog Server with Guid: {clientGuid}");
                    ViewerFiltersHandler.SendLogMessages(logMessages);
                    break;
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
        private static void ViewerFiltersHandler_OnViewerFiltersChanged(Guid changedViewer, Dictionary<Guid, List<FilterCriteria>> currentFilters)
        {
            List<FilterCriteria> changedViewerFilters = null;
            if (currentFilters.TryGetValue(changedViewer, out changedViewerFilters))
            {
                foreach (var changedViewerFilter in changedViewerFilters)
                {
                    if (changedViewerFilter.State == FilterCriteriaState.Search && !String.IsNullOrWhiteSpace(_configuration.TimelogServerLogFileDirectory))
                    {
                        //Async call to the File system to get the log messages that match the filter
                        Task.Run(() =>
                        {
                            var logMessages = LogMessageFileHandler.SearchLogFiles(changedViewerFilter, _configuration.TimelogServerLogFileDirectory);
                            ViewerServer.SendLogMessages(changedViewer, logMessages);
                        });
                    }
                }
            }

            //Update the server with the new filters
            _client.SetFilter(currentFilters.Values.SelectMany(fc => fc.ToList()).ToList());
        }
    }

    internal class LogServerClientConfiguration
    {
        public WatsonTcpClientConfiguration WatsonTCPClientConfiguration { get; set; }

        public string TimelogServerLogFileDirectory { get; set; }
    }
}
