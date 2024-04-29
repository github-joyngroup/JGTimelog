using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using Joyn.Timelog.Common;
using Joyn.Timelog.Common.Models;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Joyn.Timelog.Server.Viewers;

namespace Joyn.Timelog.Server
{
    /// <summary>
    /// Listens for LogMessages within an UDP socket and handles the received data
    /// Will check if the application key is authorized and add the received data to a round robin queue
    /// Will dump the queue to a log file periodically
    /// </summary>
    public class UDPListener : BackgroundService
    {
        //Configuration Members
        /// <summary>Logger to use</summary>
        private static ILogger _logger;

        /// <summary>Configuration client to use</summary>
        private static UDPListenerConfiguration _configuration;

        /// <summary>UDP Socket that will be listening for messages</summary>
        private static UdpClient _server;
        
        /// <summary>The client endpoint that will be receiving the messages - Will be mapped to IPAddress.Any</summary>
        private static IPEndPoint _clientEndPoint;


        //Execution Members
        /// <summary>The main queue that will hold the received data</summary>
        public static RoundRobinArray<LogMessage>? ReceivedDataQueue;

        /// <summary>An hashset containing the Guids of the applications that can submit logs</summary>
        private static HashSet<Guid> AcceptedApplicationKeys;
        
        /// <summary>The cancellation token source to stop the listener - will be flagged on the stop method</summary>
        private static CancellationTokenSource StoppingCancelationTokenSource;


        /// <summary>
        /// Setup the server, load configuration and prepare the server to start listening
        /// </summary>
        public static void Startup(UDPListenerConfiguration configuration, ILogger logger)
        {
            _configuration = configuration;
            _logger = logger;

            AcceptedApplicationKeys = LoadAuthorizedAppKeys();

            //initialize the queue manager to handle the received data
            //queueManager = new QueueManager(configuration.InternalCacheMaxEntries);
            ReceivedDataQueue = new RoundRobinArray<LogMessage>(_configuration.CacheConfiguration.InternalCacheMaxEntries);

            //initialize the UDP server
            _server = new UdpClient(_configuration.UDPSocketConfiguration.TimelogServerPort);
            _clientEndPoint = new IPEndPoint(IPAddress.Any, _configuration.UDPSocketConfiguration.TimelogServerPort);

            _logger?.LogInformation($"Timelog.Server setup.");
        }

        /// <summary>
        /// Load the authorized clients from the path defined configuration file, 
        /// if path or file not present, will load directly from the configuration
        /// </summary>
        public static HashSet<Guid> LoadAuthorizedAppKeys()
        {
            var baseAcceptedApplicationKeys = new List<Guid>();

            try
            {
                if (!string.IsNullOrEmpty(_configuration?.AuthorizedAppKeysFilePath) && File.Exists(_configuration.AuthorizedAppKeysFilePath))
                {
                    var lines = File.ReadAllLines(_configuration.AuthorizedAppKeysFilePath);

                    foreach (var line in lines)
                    {
                        Guid candidateGuid = Guid.Empty;
                        if (Guid.TryParse(line, out candidateGuid))
                        {
                            baseAcceptedApplicationKeys.Add(candidateGuid);
                        }
                    }
                }
                else if (_configuration?.AuthorizedAppKeysDirect is not null)
                {
                    baseAcceptedApplicationKeys = _configuration.AuthorizedAppKeysDirect;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error loading authorized clients: {ex.Message}");
            }

            if (baseAcceptedApplicationKeys is null || baseAcceptedApplicationKeys.Count == 0)
            {
                var errorMsg = $"No authorized clients found. Please check the configuration file or the authorized apps file.";
                _logger.LogError(errorMsg);
                throw new InvalidOperationException(errorMsg);
            }

            return baseAcceptedApplicationKeys.ToHashSet();
        }

        /// <summary>
        /// Starts the UDPListener
        /// </summary>
        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger?.LogInformation($"Timelog.Server is starting...");

            StoppingCancelationTokenSource = new CancellationTokenSource();
            
            //Start listening to the UDP port
            new Thread(() => Listening(StoppingCancelationTokenSource.Token)).Start();

            return Task.CompletedTask;
        }

        /// <summary>
        /// Stops the UDPListener
        /// </summary>
        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger?.LogInformation($"Timelog.Server is stopping.");

            StoppingCancelationTokenSource.Cancel();
            await Task.Run(() =>
            {
                _logger.LogInformation($"Stopping listening on port UDP:{_configuration.UDPSocketConfiguration.TimelogServerPort}.");
                _server?.Close();

            }, CancellationToken.None);

            await base.StopAsync(StoppingCancelationTokenSource.Token);
        }


        /// <summary>
        /// Listen to the UDP port and handle the received data from authorized clients
        /// </summary>
        /// <param name="logHandler">Handler to work the received LogMessage</param>
        /// <param name="cancellationToken">Cancellation token to cancel the reception of data</param>
        /// <returns></returns>
        public static void Listening(CancellationToken cancellationToken)
        {
            _logger.LogInformation($"Timelog.Server is listening on port UDP:{_configuration.UDPSocketConfiguration.TimelogServerPort}.");
            //StreamWriter _streamWriter = new StreamWriter("C:\\TEMP\\TimelogFiltered\\Timelog_filtered.txt");

            List<LogViewer> currentLogViewers = ViewersServer.CloneCurrentLogViewers();
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (ViewersServer.LogViewersDirty)
                    {
                        currentLogViewers = ViewersServer.CloneCurrentLogViewers();
                    }
                    //Receive the data from the UDP port
                    var receivedData = _server?.Receive(ref _clientEndPoint);

                    if (receivedData is null) { continue; }

                    //Deserialize the received data
                    var receivedLogMessage = ProtoBufSerializer.Deserialize<LogMessage>(receivedData);
                    //var receivedLogMessage = BinaronSerializer<LogMessage>.Deserialize(receivedData);

                    //Check if the application key is authorized - if not, ignore the message by continuing the loop
                    if (!AcceptedApplicationKeys.Contains(receivedLogMessage.ApplicationKey)) { continue; }

                    //Stamp the received data with current UTC time
                    receivedLogMessage.TimeServerTimeStamp = DateTime.UtcNow;

                    //Flag filters interested in the message
                    foreach (var logViewer in currentLogViewers)
                    {
                        if (logViewer.Filters != null && logViewer.Filters.Any())
                        {
                            foreach (var filter in logViewer.Filters)
                            {
                                //if Filter State is not ON, continue
                                if(filter.State != FilterCriteriaState.On) { continue; }

                                if (filter.Matches(receivedLogMessage))
                                {
                                    receivedLogMessage.FilterBitmask |= logViewer.Bitmask;
                                    break;
                                }
                            }
                        }
                    }

                    //Add the received data to the queue
                    int auxCidx = ReceivedDataQueue.Add(receivedLogMessage);

                    if ((auxCidx >= LogFileManager.LastDumpToFileIndex + LogFileManager.FlushItemsSize || //Already have more than FlushItemsSize items
                        auxCidx < LogFileManager.LastDumpToFileIndex)) //Round robined - could calculate the difference between the two indexes but will just to an extra dump file when round robined
                    {
                        LogFileManager.Pulse();
                    }

                    if ((auxCidx >= ViewersServer.LastDumpToViewersIndex + ViewersServer.FlushItemsSize || //Already have more than FlushItemsSize items
                        auxCidx < ViewersServer.LastDumpToViewersIndex)) //Round robined - could calculate the difference between the two indexes but will just to an extra dump file when round robined
                    {
                        ViewersServer.Pulse();
                    }
                }
                catch (Exception e)
                {
                    _logger?.LogError($"Timelog.Server error occurred: {e.Message}");
                }
            }
        }

        ///Moved to LogFileManager
        //private static void FlushToDiskTimer(CancellationToken cancellationToken)
        //{
        //    while (!cancellationToken.IsCancellationRequested)
        //    {
        //        Thread.Sleep(_configuration.LogFileManagerConfiguration.FlushTimeSeconds * 1000);
        //        if(DateTime.UtcNow - LastDumpToFileMoment > TimeSpan.FromSeconds(_configuration.LogFileManagerConfiguration.FlushTimeSeconds))
        //        {
        //            ForceFlushToDisk = true;
        //        }
        //    }
        //}
    }
 
    public class UDPListenerConfiguration
    {
        /// <summary>
        /// The path of a json file with the Access Control List (ACL), of the clients that will be authorized to communicate with the server.
        /// </summary>
        public string AuthorizedAppKeysFilePath { get; set; }

        /// <summary>
        /// Other way to configure the list of Guids that can communicate with the server.
        /// </summary>
        public List<Guid> AuthorizedAppKeysDirect { get; set; }

        public UDPListenerUDPSocketConfiguration UDPSocketConfiguration { get; set; }
        public UDPListenerCacheConfiguration CacheConfiguration { get; set; }
    }
    
    public class UDPListenerUDPSocketConfiguration
    {
        /// <summary>
        /// The Timelog server network port number, that will be open to receive the logs sent by the client. Default is 7771.
        /// </summary>
        public int TimelogServerPort { get; set; } = 7771;
    }

    public class UDPListenerCacheConfiguration
    {
        /// <summary>
        /// The number of entries to be accepted on the global internal cache. Default is 1000000 (1 Million).
        /// </summary>
        public int InternalCacheMaxEntries { get; set; } = 1000000;
    }
}
