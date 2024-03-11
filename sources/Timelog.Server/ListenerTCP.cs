//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using System.Net;
//using System.Net.Sockets;
//using System.Text;
//using System.Threading;
//using Microsoft.Extensions.Logging;
//using Timelog.Common;
//using Timelog.Common.Models;

//namespace Timelog.Server
//{
//    public static class ListenerTCP
//    {
//        private static ILogger? _logger;
//        private static Configuration? ServerConfiguration;
//        private static TcpListener? tcpListener;
//        private static TcpClient? tcpClient;

//        static void Main2(string[] args)
//        {
//#warning This is a temporary main method to test the Listener class. TODO: Load the configuration from a file.
//            var configuration = new Configuration
//            {
//                AuthorizedAppKeys = new List<Guid>
//                {
//                    Guid.Parse("412dbc5b-96dc-46cc-940e-ec42452be4f2"),
//                },
//            };

//            Startup(configuration, null, new System.Threading.CancellationToken());
//        }

//        /// <summary>
//        /// Start the server, listen to the TCP port, and handle the received data
//        /// </summary>
//        /// <param name="configuration"></param>
//        /// <param name="logger"></param>
//        /// <param name="cancellationToken"></param>
//        public static async void Startup(Configuration configuration, ILogger logger, CancellationToken cancellationToken)
//        {
//            ServerConfiguration = configuration;
//            _logger = logger;

//            LoadAuthorizedClients();

//            tcpListener = new TcpListener(IPAddress.Any, configuration.TimelogServerPort);
//            tcpListener.Start();

//            Console.WriteLine($"Timelog.Server is listening on port {configuration.TimelogServerPort}.");

//            Listening(null, cancellationToken);
//        }

//        /// <summary>
//        /// Load the authorized clients from the configuration file
//        /// </summary>
//        private static void LoadAuthorizedClients()
//        {
//            if (!string.IsNullOrEmpty(ServerConfiguration?.AuthorizationsFilePath) && File.Exists(ServerConfiguration.AuthorizationsFilePath))
//            {
//                var authorizedClients = File.ReadAllLines(ServerConfiguration.AuthorizationsFilePath);
//                ServerConfiguration.AuthorizedAppKeys = new List<Guid>();
//                ServerConfiguration.AuthorizedAppKeys.AddRange(authorizedClients.Select(Guid.Parse).ToList());
//            }
//            else if (ServerConfiguration?.AuthorizedAppKeys is not null)
//            {
//                ServerConfiguration.AuthorizedAppKeys = ServerConfiguration.AuthorizedAppKeys;
//            }
//        }

//        /// <summary>
//        /// Listen to the TCP port and handle the received data from authorized clients
//        /// </summary>
//        /// <param name="logHandler">Handler to work with the received LogMessage</param>
//        /// <param name="cancellationToken">Cancellation token to cancel the reception of data</param>
//        private static void Listening(Action<LogMessage> logHandler, CancellationToken cancellationToken)
//        {
//            int i = 0;


//            // Accept incoming connection



//            while (!cancellationToken.IsCancellationRequested)
//            {
//                try
//                {
//                    // Receive the data from the TCP client
//                    tcpClient = tcpListener?.AcceptTcpClient();
//                    if (tcpClient is null) { continue; }
//                    using (var stream = tcpClient.GetStream())
//                    using (var reader = new StreamReader(stream, Encoding.UTF8))
//                    {
//                        var receivedData = reader.ReadToEnd();

//                        // Deserialize the received data
//                        var receivedLogMessage = System.Text.Json.JsonSerializer.Deserialize<LogMessage>(receivedData);

//                        // Stamp the received data with the current UTC time
//                        receivedLogMessage.TimeServerTimeStamp = DateTime.UtcNow;

//                        // Check if the application key is authorized, and if so, handle the received data
//                        if (IsAuthorized(receivedLogMessage.ApplicationKey))
//                        {
//                            logHandler(receivedLogMessage);
//                        }

//                        Console.WriteLine($"{i} --> {System.Text.Json.JsonSerializer.Serialize(receivedLogMessage)[..100]}...");

//                        Timelog.TestClientEfficiency.Program.ProcessLogMessage(receivedLogMessage);
//                    }
//                }
//                catch (Exception e)
//                {
//                    _logger?.LogError($"Timelog.Server error occurred: {e.Message}");
//                    Console.WriteLine($"Timelog.Server error occurred: {e.Message}");
//                }
//                i++;
//            }
//        }

//        /// <summary>
//        /// Check if the application key is authorized
//        /// </summary>
//        /// <param name="clientAppKey">Application key to check</param>
//        /// <returns></returns>
//        private static bool IsAuthorized(Guid clientAppKey)
//        {
//            return ServerConfiguration?.AuthorizedAppKeys is not null && ServerConfiguration.AuthorizedAppKeys.Contains(clientAppKey);
//        }
//    }
//}
