using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;
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
            ClientConfiguration = configuration;
            _logger = logger;

            udpClient = new UdpClient();
            udpClient.Connect(ClientConfiguration.TimelogServerHost, ClientConfiguration.TimelogServerPort);

            _logger?.LogInformation($"Timelog.Client '{ClientConfiguration.ApplicationKey.ToString()[..4]}...' is ready to log to the server {configuration.TimelogServerHost}:{configuration.TimelogServerPort}.");
        }

        
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

    
}
