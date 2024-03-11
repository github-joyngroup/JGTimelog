using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Timelog.Common.Models;

namespace Timelog.ClientReport
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            try
            {
                var configuration = ReadConfiguration("appsettings.json", Directory.GetCurrentDirectory());
                InitializeApplication();
                RunApplication(configuration);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during application initialization: {ex.Message}");
            }
        }

        public static Configuration ReadConfiguration(string file, string filePath)
        {
            var configuration = new Configuration();
            var configurationBuilder = new ConfigurationBuilder()
                .SetBasePath(filePath)
                .AddJsonFile(file);

            IConfigurationRoot configRoot = configurationBuilder.Build();
            configRoot.GetSection("TimelogClientReportService").Bind(configuration);

            // Add additional validation if needed

            return configuration;
        }

        private static async void InitializeApplication()
        {
            // Initialize the SignalR connection
            await ClientReportLogger.Init("http://localhost:5000/logMessageHub", null); // Replace with the URL of your SignalR hub
        }

        private static async void RunApplication(Configuration configuration)
        {
            try
            {
                // Create and send log messages
                var logMessage = new LogMessage
                {
                    ApplicationKey = configuration.ApplicationKey,
                    Command = Commands.Start,
                    TransactionID = Guid.NewGuid(),
                    Message = new Message { Header = $"", Data = Encoding.UTF8.GetBytes($"") },
                };

                const int MAX = 10000;

                int i = 0;
                while (i < MAX)
                {
                    logMessage.Domain = $"{i}";

                    // Send log message
                    await ClientReportLogger.Log(logMessage);

                    i++;
                }

                // Other application logic
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during application initialization: {ex.Message}");
            }
        }
    }
}
