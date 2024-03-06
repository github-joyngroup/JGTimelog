using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Net.Sockets;
using System.Text;
using Timelog.Common;
using Timelog.Common.Models;

namespace Timelog.ClientReport
{
    public static class ClientReportLogger
    {
        private static TcpClient tcpClient;
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
            if (tcpClient != null || isInitialized)
            {
                throw new InvalidOperationException("Logger has already been initialized.");
            }

            ClientConfiguration = configuration;
            _logger = logger;

            // Instantiate tcpClient here to ensure proper initialization
            tcpClient = new TcpClient();

            try
            {
                tcpClient.Connect(configuration.TimelogServerHost, configuration.TimelogServerPort);
                _logger?.LogInformation($"Timelog.ClientReport '{configuration.ApplicationKey.ToString()[..4]}...' is ready to log to the server {configuration.TimelogServerHost}:{configuration.TimelogServerPort}.");

                isInitialized = true;
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error connecting to the server: {ex.Message}");
                throw;
            }
        }

        // Existing Log method
        public static void Log(LogLevel logLevel, LogMessage message)
        {
            if (((int)logLevel) >= ClientConfiguration.LogLevel && message != null)
            {
                string logString = JsonConvert.SerializeObject(message);

                try
                {
                    if (ClientConfiguration.UseClientTimestamp)
                    {
                        message.OriginTimestamp = DateTime.UtcNow;
                    }

                    byte[] data = Encoding.UTF8.GetBytes(logString);
                    tcpClient.GetStream().Write(data, 0, data.Length);
                }
                catch (Exception ex)
                {
                    // Handle exception (log or rethrow)
                    _logger?.LogError($"Error sending log message to server: {ex.Message}");
                }
            }
        }
    }
}
