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

namespace Timelog.Server
{
    public static class Listener
    {
        private static ILogger? _logger;
        private static Configuration? ServerConfiguration;
        private static UdpClient? udpServer;
        private static List<Guid>? acceptedApplicationKeys;
        private static IPEndPoint? clientEndPoint;
        private static QueueManager? queueManager;
        private static LogFileManager? logFileManager;
        private static RoundRobinArray<LogMessage>? receivedDataQueue;

        static void Main(string[] args)
        {
#warning This is a temporary main method to test the Listener class. TODO: Load the configuration from a file.
            var configuration = new Configuration
            {
                AuthorizedAppKeys = new List<Guid>
                {
                    Guid.Parse("8ce94d5e-b2a3-4685-9e6c-ab21410b595f"),
                },
            };

            Startup(configuration, null);

            
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

            var t = new Thread(() => Listening(null, cancellationTokenSource.Token));
                t.Start();

            Thread.Sleep(25000);
            

            //Listening(queueManager.LogHandler, new System.Threading.CancellationToken());
            //Listening(null, new System.Threading.CancellationToken());

            //Dump the receivedDataQueue to the log file
            logFileManager?.DumpToFile(0, receivedDataQueue.GetItems());
            //logFileManager?.DumpToFileBinary(0, receivedDataQueue.GetItems());
        }
                

        /// <summary>
        /// Start the server, listen to the UDP port, and handle the received data
        /// </summary>
        /// <param name="configuration"></param>
        /// <param name="logger"></param>
        /// <param name="cancellationToken"></param>
        public static async void Startup(Configuration configuration, ILogger logger)
        {
            ServerConfiguration = configuration;
            _logger = logger;
            
            LoadAuthorizedClients();

            //initialize the queue manager to handle the received data
            //queueManager = new QueueManager(configuration.InternalCacheMaxEntries);
            receivedDataQueue = new RoundRobinArray<LogMessage>(configuration.InternalCacheMaxEntries);

            //initialize the log file manager to handle the log files
            logFileManager = new LogFileManager(configuration.LogFilesPath, configuration.MaxLogFiles, configuration.MaxLogFileEntries);

            udpServer = new UdpClient(configuration.TimelogServerPort);
            clientEndPoint = new IPEndPoint(IPAddress.Any, ServerConfiguration.TimelogServerPort);

            Console.WriteLine($"Timelog.Server is listening on port {configuration.TimelogServerPort}.");

            ////new Thread(() => Listening(queueManager.LogHandler, cancellationToken)).Start();
            //Listening(queueManager.LogHandler, cancellationToken);
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
        public static void Listening(Action<LogMessage> logHandler, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {                    
                    //Receive the data from the UDP port
                    var receivedData = udpServer?.Receive(ref clientEndPoint);
                    
                    if (receivedData is null) { continue; }

                    //Deserialize the received data
                    var receivedLogMessage = ByteSerializer<LogMessage>.Deserialize(receivedData);
                    
                    //Stamp the received data with current UTC time
                    receivedLogMessage.TimeServerTimeStamp = DateTime.UtcNow;

                    //Check if the application key is authorized, and if so, handle the received data
                    if (IsAuthorized(receivedLogMessage.ApplicationKey))
                    {
                        receivedDataQueue?.Add(receivedLogMessage);
                        //logHandler(receivedLogMessage);
                    }

                    //Console.WriteLine($"{i} --> {System.Text.Json.JsonSerializer.Serialize(receivedLogMessage)[..100]}...");
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

    }
        
}
