using Microsoft.Extensions.Logging;
using System;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using Timelog.Common;
using Timelog.Common.Models;

namespace Timelog.Client
{
    public static class Logger
    {
        private static UdpClient udpClient;
        private static ILogger _logger;
        private static Configuration ClientConfiguration;
        public static void Startup(Configuration configuration, ILogger logger)
        {
            return ClientConfiguration;
        }


        // Initialization constructor
        public static void Init(Configuration configuration, ILogger logger)
        {
            // Ensure that Init can only be called once
            if (udpClient != null || isInitialized)
            {
                throw new InvalidOperationException("Logger has already been initialized.");
            }

            ClientConfiguration = configuration;
            _logger = logger;

            // Instantiate udpClient here to ensure proper initialization
            udpClient = new UdpClient();
            udpClient.Connect(ClientConfiguration.TimelogServerHost, ClientConfiguration.TimelogServerPort);

            _logger?.LogInformation($"Timelog.Client '{ClientConfiguration.ApplicationKey.ToString()[..4]}...' is ready to log to the server {configuration.TimelogServerHost}:{configuration.TimelogServerPort}.");
        }

        // Existing Log method
        public static void Log(LogLevel logLevel, LogMessage message)
        {
            
            if (((int)message.LogLevelClient) >= (int)ClientConfiguration.LogLevel)  //&& message != null)
            {
                udpClient.Send(logBytes, logBytes.Length);

                //if(logBytes.Length > 1500)
                //{
                //    _logger?.LogWarning($"Timelog.Client '{ClientConfiguration.ApplicationKey.ToString()[..4]}...' sent a log message that is larger than 1500 bytes. This may cause fragmentation and performance issues.");
                //}
            }
			
           
        }

        // Binary serialization method
        private static byte[] SerializeToBinary(object obj)
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                BinaryFormatter binaryFormatter = new BinaryFormatter();
                binaryFormatter.Serialize(memoryStream, obj);
                return memoryStream.ToArray();
            }
        }
    }
}