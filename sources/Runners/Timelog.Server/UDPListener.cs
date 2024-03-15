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
using Timelog.Common;
using Timelog.Common.Models;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Timelog.Server.Search;
using Timelog.Server.Viewers;

namespace Timelog.Server
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
        
        /// <summary>The file manager that will periodically dump information to the file system</summary>
        private static LogFileManager LogFileManager;

        /// <summary>The cancellation token source to stop the listener - will be flagged on the stop method</summary>
        private static CancellationTokenSource StoppingCancelationTokenSource;


        //Control members
        /// <summary>Current File Dump index</summary>
        private static int LastDumpToFileIndex= 0;
        /// <summary>When did the last dump to file happened</summary>
        private static DateTime? LastDumpToFileMoment = DateTime.UtcNow;
        /// <summary>Force a flush to the disk on the next iteration</summary>
        private static bool ForceFlushToDisk = false;


        /// <summary>
        /// Setup the server, load configuration and prepare the server to start listening
        /// </summary>
        public static void Startup(UDPListenerConfiguration configuration, ILogger logger)
        {
            _configuration = configuration;
            _logger = logger;

            AcceptedApplicationKeys = ViewersServer.GetAuthorizedAppKeys();

            //initialize the queue manager to handle the received data
            //queueManager = new QueueManager(configuration.InternalCacheMaxEntries);
            ReceivedDataQueue = new RoundRobinArray<LogMessage>(_configuration.CacheConfiguration.InternalCacheMaxEntries);

            //initialize the log file manager to handle the log files
            LogFileManager = new LogFileManager(ReceivedDataQueue, _configuration.LogFileManagerConfiguration.LogFilesPath, _configuration.LogFileManagerConfiguration.MaxLogFiles, _configuration.LogFileManagerConfiguration.MaxLogFileEntries);

            //initialize the UDP server
            _server = new UdpClient(_configuration.UDPSocketConfiguration.TimelogServerPort);
            _clientEndPoint = new IPEndPoint(IPAddress.Any, _configuration.UDPSocketConfiguration.TimelogServerPort);

            _logger?.LogInformation($"Timelog.Server setup.");
        }

        /// <summary>
        /// Starts the UDPListener and the LogFileManager thread
        /// </summary>
        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger?.LogInformation($"Timelog.Server is starting...");

            StoppingCancelationTokenSource = new CancellationTokenSource();
            
            //Start listening to the UDP port
            new Thread(() => Listening(StoppingCancelationTokenSource.Token)).Start();
            new Thread(() => FlushToDiskTimer(StoppingCancelationTokenSource.Token)).Start();

            return Task.CompletedTask;
        }

        /// <summary>
        /// Stops the UDPListener and the LogFileManager thread
        /// </summary>
        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger?.LogInformation($"Timelog.Server is stopping.");

            StoppingCancelationTokenSource.Cancel();
            await Task.Run(() =>
            {
                _logger.LogInformation($"Stopping listening on port UDP:{_configuration.UDPSocketConfiguration.TimelogServerPort}.");
                _server?.Close();
                
                //Dump the last received data to the log file and close the log file manager
                LogFileManager?.DumpFilesPeriodically(ReceivedDataQueue.CurrentIndex, LastDumpToFileIndex);
                LogFileManager?.Close();

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

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {                    
                    //Receive the data from the UDP port
                    var receivedData = _server?.Receive(ref _clientEndPoint);
                    
                    if (receivedData is null) { continue; }

                    //Deserialize the received data
                    var receivedLogMessage = ByteSerializer<LogMessage>.Deserialize(receivedData);
                    //var receivedLogMessage = BinaronSerializer<LogMessage>.Deserialize(receivedData);

                    //Check if the application key is authorized - if not, ignore the message by continuing the loop
                    if (!AcceptedApplicationKeys.Contains(receivedLogMessage.ApplicationKey)) { continue; }

                    //Stamp the received data with current UTC time
                    receivedLogMessage.TimeServerTimeStamp = DateTime.UtcNow;

                    //Flag filters interested in the message
                    //*****************************
                    //********** TODO EP ********** 
                    //*****************************

                    //Add the received data to the queue
                    ReceivedDataQueue?.Add(receivedLogMessage);
                    int auxCidx = ReceivedDataQueue.CurrentIndex - 1;
                    //LogMessageSearch.DumpSearched(auxCidx, _streamWriter);
                        
                    if (auxCidx >= LastDumpToFileIndex + _configuration.LogFileManagerConfiguration.FlushItemsSize || ForceFlushToDisk)
                    {
                        ForceFlushToDisk = false;
                        int auxLidf = LastDumpToFileIndex;
                        //int auxCidx = ReceivedDataQueue.CurrentIndex-1;
                        Task.Run(() => LogFileManager?.DumpFilesPeriodically(auxCidx, auxLidf));

                        LastDumpToFileIndex = ReceivedDataQueue.CurrentIndex;
                        LastDumpToFileMoment = receivedLogMessage.TimeServerTimeStamp;
                        ForceFlushToDisk = false;
                    }
                    //Used in debug to make sure messages are incoming
                    //if (ReceivedDataQueue.CurrentIndex % 1500 == 0)
                    //{
                    //    _logger.LogInformation($"CurrentIndex: {ReceivedDataQueue.CurrentIndex}");
                    //}
                }
                catch (Exception e)
                {
                    _logger?.LogError($"Timelog.Server error occurred: {e.Message}");
                }
            }
        }

        private static void FlushToDiskTimer(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                Thread.Sleep(_configuration.LogFileManagerConfiguration.FlushTimeSeconds * 1000);
                if(DateTime.UtcNow - LastDumpToFileMoment > TimeSpan.FromSeconds(_configuration.LogFileManagerConfiguration.FlushTimeSeconds))
                {
                    ForceFlushToDisk = true;
                }
            }
        }
    }
 
    public class UDPListenerConfiguration
    {
        public UDPListenerUDPSocketConfiguration UDPSocketConfiguration { get; set; }
        public UDPListenerCacheConfiguration CacheConfiguration { get; set; }
        public UDPListenerLogFileManager LogFileManagerConfiguration { get; set; }
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
        /// The number of entries to be accepted on the global internal cache. Default is 1000000.
        /// </summary>
        public int InternalCacheMaxEntries { get; set; } = 1000000;
    }

    public class UDPListenerLogFileManager
    {
        /// <summary>
        /// The path of the log files. Default is the "Timelog" folder inside the current user temporary folder.
        /// </summary>
        public string LogFilesPath { get; set; } = Path.Combine(Path.GetTempPath(), "Timelog");

        /// <summary>
        /// The maximum number of log files to be created. Log files are rotating. Default is 10.
        /// </summary>
        public int MaxLogFiles { get; set; } = 10;

        /// <summary>
        /// The maximum number of entries per log file. Default is 100000.
        /// </summary>
        public int MaxLogFileEntries { get; set; } = 100000;

        /// <summary>
        /// The number of entries that force a flush to log file. Default is 20000.
        /// </summary>
        public int FlushItemsSize { get; set; } = 20000;

        /// <summary>
        /// The number of seconds that force a flush to log file. Default is 30 seconds.
        /// </summary>
        public int FlushTimeSeconds { get; set; } = 30;
    }
}
