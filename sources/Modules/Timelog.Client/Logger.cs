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
        private static LoggerConfiguration _configuration;

        public static void Startup(LoggerConfiguration configuration, ILogger logger)
        {
            _configuration = configuration;
            _logger = logger;

            udpClient = new UdpClient();
            udpClient.Connect(_configuration.TimelogServerHost, _configuration.TimelogServerPort);

            _logger?.LogInformation($"Timelog.Client...' is ready to log to the server {_configuration.TimelogServerHost}:{_configuration.TimelogServerPort}.");
        }

        // Existing Log method
        public static void Log(LogLevel logLevel, LogMessage message)
        {
            byte[] logBytes = ByteSerializer<LogMessage>.Serialize(message);

            try
            {
                udpClient.Send(logBytes, logBytes.Length);

                //if(logBytes.Length > 1500)
                //{
                //    _logger?.LogWarning($"Timelog.Client '{ClientConfiguration.ApplicationKey.ToString()[..4]}...' sent a log message that is larger than 1500 bytes. This may cause fragmentation and performance issues.");
                //}
            }
            catch
            {
            }
        }
    }

    public class LoggerConfiguration
    {
        /// <summary>
        /// The FQDN of the Timelog server or their IP address
        /// </summary>
        public string TimelogServerHost { get; set; }

        /// <summary>
        /// The Timelog server network port number
        /// </summary>
        public int TimelogServerPort { get; set; }

        /// <summary>
        /// The Timelog level
        /// </summary>
        public LogLevel LogLevel { get; set; }

        /// <summary>
        /// Client Timestamp
        /// </summary>
        public bool UseClientTimestamp { get; set; }
    }
}