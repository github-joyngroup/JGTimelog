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
        /// <summary>
        /// The unique application key to be used in the TCP communication
        /// </summary>
        private static Guid _applicationKey; 

        /// <summary>
        /// The logger instance
        /// </summary>
        private static ILogger _logger;

        /// <summary>
        /// The configuration of the logger
        /// </summary>
        private static LoggerConfiguration _configuration;

        /// <summary>
        /// Socket UDP to use for logging
        /// </summary>
        private static UdpClient udpClient;

        public static void Startup(Guid applicationKey, LoggerConfiguration configuration, ILogger logger)
        {
            _configuration = configuration;
            _logger = logger;
            _applicationKey = applicationKey;

            udpClient = new UdpClient();
            udpClient.Connect(_configuration.TimelogServerHost, _configuration.TimelogServerPort);

            _logger?.LogInformation($"Timelog.Client...' is ready to log to the server {_configuration.TimelogServerHost}:{_configuration.TimelogServerPort}.");
        }

        //Log Methods
        public static void Log(LogLevel logLevel, int domain, Guid transactionId, long? clientTag = null)
        {
            LogMessage logMessage = new LogMessage()
            {
                Domain = domain,
                ClientLogLevel = (int)logLevel,
                ClientTag = clientTag ?? 0,
                TransactionID = transactionId,
                Command = Commands.Normal,
                OriginTimestamp = _configuration.UseClientTimestamp ? DateTime.UtcNow : null
            };

            Log(logMessage);
        }


        public static LogMessage LogStart(LogLevel logLevel, int domain, Guid transactionId, long? clientTag = null)
        {
            LogMessage logMessage = new LogMessage()
            {
                Domain = domain,
                ClientLogLevel = (int)logLevel,
                ClientTag = clientTag ?? 0,
                TransactionID = transactionId,
                Command = Commands.Start,
                OriginTimestamp = DateTime.UtcNow
            };

            return Log(logMessage);
        }

        public static void LogStop(LogMessage startLogMessage)
        {
            LogMessage logMessage = new LogMessage()
            {
                Domain = startLogMessage.Domain,
                ClientLogLevel = startLogMessage.ClientLogLevel,
                ClientTag = startLogMessage.ClientTag,
                TransactionID = startLogMessage.TransactionID,
                Command = Commands.Stop,
                OriginTimestamp = DateTime.UtcNow,
                ExecutionTime = DateTime.UtcNow - startLogMessage.OriginTimestamp
            };

            Log(logMessage);
        }

        /// <summary>
        /// Sends the LogMessage to the UDP channel
        /// </summary>
        private static LogMessage Log(LogMessage message)
        {
            message.ApplicationKey = _applicationKey;
            byte[] logBytes = ProtoBufSerializer.Serialize(message);
            try
            {
                udpClient.Send(logBytes, logBytes.Length);
            }
            catch
            {
            }

            return message;
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