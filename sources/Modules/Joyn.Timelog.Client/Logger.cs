﻿using Microsoft.Extensions.Logging;
using System;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using Joyn.Timelog.Common;
using Joyn.Timelog.Common.Models;

namespace Joyn.Timelog.Client
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

        /// <summary>
        /// Starts the Timelog.Client based on the configuration and the application key
        /// </summary>
        public static void Startup(Guid applicationKey, LoggerConfiguration configuration, ILogger logger)
        {
            _configuration = configuration;
            _logger = logger;
            _applicationKey = applicationKey;

            if (_configuration.Disabled)
            {
                _logger?.LogInformation($"Timelog.Client is disabled at config no messages will be sent...");
            }

            udpClient = new UdpClient();
            udpClient.Connect(_configuration.TimelogServerHost, _configuration.TimelogServerPort);

            _logger?.LogInformation($"Timelog.Client is ready to log to the server {_configuration.TimelogServerHost}:{_configuration.TimelogServerPort}.");
        }

        ///<summary>
        /// Creates and sends a log message to the server, will use the Normal command as the default one
        /// </summary>
        public static void Log(LogLevel logLevel, uint domain, Guid transactionId, Guid? executionId = null, long? clientTag = null)
        {
            //Not Started Up or disabled
            if (_configuration == null || _configuration.Disabled) { return; }

            LogMessage logMessage = new LogMessage()
            {
                Domain = domain,
                ClientLogLevel = (int)logLevel,
                ClientTag = clientTag ?? 0,
                TransactionId = transactionId,
                ExecutionId = executionId,
                Command = Commands.Normal,
                OriginTimestamp = _configuration.UseClientTimestamp ? DateTime.UtcNow : null
            };

            Log(logMessage);
        }

        /// <summary>
        /// Will create a log message with the Start command and send it to the server, the log message will be returned to be used in the corresponding Stop Command
        /// </summary>
        public static LogMessage LogStart(LogLevel logLevel, uint domain, Guid transactionId, Guid? executionId = null, long? clientTag = null)
        {
            //Not Started Up or disabled
            if (_configuration == null || _configuration.Disabled) { return new LogMessage(); }

            LogMessage logMessage = new LogMessage()
            {
                Domain = domain,
                ClientLogLevel = (int)logLevel,
                ClientTag = clientTag ?? 0,
                TransactionId = transactionId,
                ExecutionId = executionId,
                Command = Commands.Start,
                OriginTimestamp = DateTime.UtcNow
            };

            return Log(logMessage);
        }

        /// <summary>
        /// Will create a stop message based on the start message and send it to the server
        /// </summary>
        public static void LogStop(LogMessage startLogMessage, long? clientTag = null)
        {
            //Not Started Up or disabled
            if (_configuration == null || _configuration.Disabled) { return; }

            LogMessage logMessage = new LogMessage()
            {
                Domain = startLogMessage.Domain,
                ClientLogLevel = startLogMessage.ClientLogLevel,
                ClientTag = clientTag ?? startLogMessage.ClientTag,
                TransactionId = startLogMessage.TransactionId,
                ExecutionId = startLogMessage.ExecutionId,
                Command = Commands.Stop,
                OriginTimestamp = DateTime.UtcNow,
                ExecutionTime = DateTime.UtcNow - startLogMessage.OriginTimestamp
            };

            Log(logMessage);
        }

        /// <summary>
        /// Will create a log message with the Stop command and send it to the server, the log message will be returned to be used in the corresponding Stop Command
        /// There is an option DateTime command that, when filled, will be used to calculate the execution time
        /// </summary>
        public static void LogStop(LogLevel logLevel, uint domain, Guid transactionId, Guid? executionId = null, long? clientTag = null, DateTime? startTimestamp = null)
        {
            //Not Started Up or disabled
            if (_configuration == null || _configuration.Disabled) { return; }

            LogMessage logMessage = new LogMessage()
            {
                Domain = domain,
                ClientLogLevel = (int)logLevel,
                ClientTag = clientTag ?? 0,
                TransactionId = transactionId,
                ExecutionId = executionId,
                Command = Commands.Stop,
                OriginTimestamp = DateTime.UtcNow,
                ExecutionTime = startTimestamp.HasValue ? DateTime.UtcNow - startTimestamp.Value : null
            };

            Log(logMessage);
        }

        /// <summary>
        /// Sends the LogMessage to the UDP channel
        /// </summary>
        private static LogMessage Log(LogMessage message)
        {
            //Not Started Up or disabled
            if (_configuration == null || _configuration.Disabled) { return new LogMessage(); }

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

    /// <summary>
    /// Configuration for the Logger module
    /// </summary>
    public class LoggerConfiguration
    {
        /// <summary>
        /// When true, no logs will be sent to the server
        /// </summary>
        public bool Disabled { get; set; }

        /// <summary>
        /// The FQDN of the Timelog server or their IP address
        /// </summary>
        public string TimelogServerHost { get; set; }

        /// <summary>
        /// The Timelog server network port number
        /// </summary>
        public int TimelogServerPort { get; set; }

        /// <summary>
        /// Client Timestamp
        /// </summary>
        public bool UseClientTimestamp { get; set; }
    }
}