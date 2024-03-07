using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace Timelog.ClientReport
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            try
            {
                var configuration = ReadConfiguration("appsettings.json", Directory.GetCurrentDirectory());
                InitializeApplication(configuration);
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
            configRoot.Bind(configuration);

            // Add additional validation if needed

            return configuration;
        }

        private static void InitializeApplication(Configuration configuration)
        {
            ClientReportLogger.Init(configuration, null);
            // Other initialization logic if needed
        }

        private static void RunApplication(Configuration configuration)
        {
            var logMessage = new Common.Models.LogMessage
            {
                ApplicationKey = ClientReportLogger.ClientConfiguration.ApplicationKey,
                Command = Common.Models.Commands.Start,
                Domain = "",
                TransactionID = Guid.NewGuid(),
                Message = new Common.Models.Message { Header = $"", Data = Encoding.UTF8.GetBytes($"") },
                LogLevelClient = Microsoft.Extensions.Logging.LogLevel.Trace
            };

            const int MAX = 1;

            Console.WriteLine($"Start logging {MAX} messages");
            int i = 0;

            while (i < MAX)
            {
                logMessage.Domain = $"{i}";


                ClientReportLogger.Log(logMessage);

                i++;

            }

            Console.WriteLine("Enter a message to log (press Enter to log, type 'exit' to exit):");
            string input;
            do
            {
                input = Console.ReadLine();
                if (input != "exit")
                {
                    logMessage.Domain = $"{input}";

                    ClientReportLogger.Log(logMessage);
                }
            } while (input != "exit");
        }
    }
}
