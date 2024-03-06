using Microsoft.Extensions.Logging;
using System;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using Timelog.Common;
using Timelog.Common.Models;

namespace Timelog.Client
{
    public static class ClientLogger
    {
        private static UdpClient udpClient;
        private static ILogger _logger;
        internal static Configuration ClientConfiguration;
        private static bool isInitialized = false;

        public static Configuration GetClientConfiguration()
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

            _logger?.LogInformation($"Timelog.Client '{configuration.ApplicationKey.ToString()[..4]}...' is ready to log to the server {configuration.TimelogServerHost}:{configuration.TimelogServerPort}.");

            isInitialized = true;
        }

        // Existing Log method
        public static void Log(LogLevel logLevel, LogMessage message)
        {
            if (((int)logLevel) >= ClientConfiguration.LogLevel  && message != null)
            {
                byte[] logBytes = SerializeToBinary(message);

                try
                {
                    if(ClientConfiguration.UseClientTimestamp)
                    {
                        message.OriginTimestamp = DateTime.UtcNow;
                    }
                    
                    udpClient.Send(logBytes, logBytes.Length, ClientConfiguration.TimelogServerHost, ClientConfiguration.TimelogServerPort);


                }
                catch
                {
                    // Handle exception (log or rethrow)
                    _logger?.LogError("Error sending log message to server.");
                }
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