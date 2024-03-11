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

namespace Timelog.Server
{
    public class Listener : BackgroundService
    {
        private static ILogger? _logger;
        private static Configuration? ServerConfiguration;
        private static UdpClient? udpServer;
        private static List<Guid>? acceptedApplicationKeys;
        private static IPEndPoint? clientEndPoint;
        private static LogFileManager? logFileManager;
        private static CancellationTokenSource _stoppingSource;
        private static int LastIndexDumpedtofile = 0;
        private static DateTime? LastTimeDumpedtofile = DateTime.UtcNow;
        private static bool ForceFlushToDisk = false;
        
        public static RoundRobinArray<LogMessage>? ReceivedDataQueue;

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _stoppingSource = new CancellationTokenSource();
            
            //Start listening to the UDP port
            new Thread(() => Listening(_stoppingSource.Token)).Start();
            new Thread(() => FlushToDiskTimer()).Start();
            return Task.CompletedTask;
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _stoppingSource.Cancel();
            await Task.Run(() =>
            {
                Console.WriteLine($"Timelog.Server is being stopped of listening on port UDP:{ServerConfiguration.TimelogServerPort}.");
                udpServer?.Close();
                
                //Dump the last received data to the log file
                logFileManager?.DumpFilesPeriodically(ReceivedDataQueue.CurrentIndex, LastIndexDumpedtofile);
                logFileManager?.Close();

            }, CancellationToken.None);

            await base.StopAsync(_stoppingSource.Token);
        }

        /// <summary>
        /// Start the server, listen to the UDP port, and handle the received data
        /// </summary>
        /// <param name="configuration"></param>
        /// <param name="logger"></param>
        /// <param name="cancellationToken"></param>
        public static void Startup(Configuration configuration)
        {
            ServerConfiguration = configuration;
            //_logger = logger;
            
            LoadAuthorizedClients();

            //initialize the queue manager to handle the received data
            //queueManager = new QueueManager(configuration.InternalCacheMaxEntries);
            ReceivedDataQueue = new RoundRobinArray<LogMessage>(configuration.InternalCacheMaxEntries);

            //initialize the log file manager to handle the log files
            logFileManager = new LogFileManager(ReceivedDataQueue, configuration.LogFilesPath, configuration.MaxLogFiles, configuration.MaxLogFileEntries);

            //initialize the UDP server
            udpServer = new UdpClient(configuration.TimelogServerPort);
            clientEndPoint = new IPEndPoint(IPAddress.Any, ServerConfiguration.TimelogServerPort);
            
        }

        /// <summary>
        /// Load the authorized clients from the configuration file
        /// </summary>
        private static void LoadAuthorizedClients()
        {
            if (!string.IsNullOrEmpty(ServerConfiguration?.AuthorizationsFilePath) && System.IO.File.Exists(ServerConfiguration.AuthorizationsFilePath))
            {
                var authorizedClients = File.ReadAllLines(ServerConfiguration.AuthorizationsFilePath);
                acceptedApplicationKeys = new List<Guid>();
                acceptedApplicationKeys.AddRange(authorizedClients.Select(Guid.Parse).ToList());
            }
            else if(ServerConfiguration?.AuthorizedAppKeys is not null)
            {
                acceptedApplicationKeys = ServerConfiguration.AuthorizedAppKeys;
            }
        }

        /// <summary>
        /// Listen to the UDP port and handle the received data from authorized clients
        /// </summary>
        /// <param name="logHandler">Handler to work the received LogMessage</param>
        /// <param name="cancellationToken">Cancellation token to cancel the reception of data</param>
        /// <returns></returns>
        public static void Listening(CancellationToken cancellationToken)
        {
            Console.WriteLine($"Timelog.Server is listening on port UDP:{ServerConfiguration.TimelogServerPort}.");
            //StreamWriter _streamWriter = new StreamWriter("C:\\TEMP\\TimelogFiltered\\Timelog_filtered.txt");

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {                    
                    //Receive the data from the UDP port
                    var receivedData = udpServer?.Receive(ref clientEndPoint);
                    
                    if (receivedData is null) { continue; }

                    //Deserialize the received data
                    var receivedLogMessage = ByteSerializer<LogMessage>.Deserialize(receivedData);
                    //var receivedLogMessage = BinaronSerializer<LogMessage>.Deserialize(receivedData);

                    //Stamp the received data with current UTC time
                    receivedLogMessage.TimeServerTimeStamp = DateTime.UtcNow;

                    //Check if the application key is authorized
                    if (IsAuthorized(receivedLogMessage.ApplicationKey))
                    {
                        //Add the received data to the queue
                        ReceivedDataQueue?.Add(receivedLogMessage);
                        int auxCidx = ReceivedDataQueue.CurrentIndex - 1;
                        //LogMessageSearch.DumpSearched(auxCidx, _streamWriter);
                        
                        if (auxCidx >= LastIndexDumpedtofile+ServerConfiguration.FlushItensSize || ForceFlushToDisk)
                        {
                            ForceFlushToDisk = false;
                            int auxLidf = LastIndexDumpedtofile;
                            //int auxCidx = ReceivedDataQueue.CurrentIndex-1;
                            Task.Run(() => logFileManager?.DumpFilesPeriodically(auxCidx, auxLidf));
                            
                            LastIndexDumpedtofile = ReceivedDataQueue.CurrentIndex;
                            LastTimeDumpedtofile = receivedLogMessage.TimeServerTimeStamp;
                            ForceFlushToDisk = false;
                        }
                    }
                    if(ReceivedDataQueue.CurrentIndex % 1500 == 0)
                    {
                        "".ToString();
                        Console.WriteLine($"CurrentIndex: {ReceivedDataQueue.CurrentIndex}");
                    }
                    //Console.WriteLine($"{i} --> {System.Text.Json.JsonSerializer.Serialize(receivedLogMessage)[..100]}...");

                    Timelog.TestClientEfficiency.Program.ProcessLogMessage(receivedLogMessage);

                }
                catch (Exception e)
                {
                    _logger?.LogError($"Timelog.Server error occurred: {e.Message}");
                    Console.WriteLine($"Timelog.Server error occurred: {e.Message}");
                }
                
            }

            
        }

        /// <summary>
        /// Check if the application key is authorized
        /// </summary>
        /// <param name="clientAppKey">Application key to check</param>
        /// <returns></returns>
        private static bool IsAuthorized(Guid clientAppKey)
        {
            return acceptedApplicationKeys is not null && acceptedApplicationKeys.Contains(clientAppKey);
        }

        private static void FlushToDiskTimer()
        {
            while (true)
            {
                Thread.Sleep(ServerConfiguration.FlushTimeSeconds * 1000);
                if(DateTime.UtcNow - LastTimeDumpedtofile > TimeSpan.FromSeconds(ServerConfiguration.FlushTimeSeconds))
                {
                    ForceFlushToDisk = true;
                }
                
            }
        }
    }
        
}
