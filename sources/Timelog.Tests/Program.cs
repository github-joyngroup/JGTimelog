using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Timelog.Client;

namespace Timelog.Tests
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
            configRoot.GetSection("TimelogTestClientService").Bind(configuration);

            // Add additional validation if needed

            return configuration;
        }

        private static void InitializeApplication(Configuration configuration)
        {
            Logger.Init(configuration, null);
        }

        private static void RunApplication(Configuration configuration)
        {
            var logMessage = new Common.Models.LogMessage
            {
                ApplicationKey = configuration.ApplicationKey,
                Command = Common.Models.Commands.Start,
                Domain = "",
                TransactionID = Guid.NewGuid(),
                OriginTimestamp = DateTime.UtcNow,
                Message = new Common.Models.Message { Header = $"", Data = Encoding.UTF8.GetBytes($"") },
            };

            const int MAX = 10000;
            
            Console.WriteLine($"Start logging {MAX} messages");
            int i = 0;
            while (i < MAX)
            {
                //convert the integer to binary and send it as the domain
                //logMessage.Domain = $"{Convert.ToString(i,2)}";
                logMessage.Domain = $"{i}";

                Client.Logger.Log(Microsoft.Extensions.Logging.LogLevel.Trace, logMessage);
                
                i++;
                //Thread.Sleep(10);
            }

            //Console.WriteLine("Enter a message to log (press Enter to log, type 'exit' to exit):");
            //string input;
            //do
            //{
            //    input = Console.ReadLine();
            //    if (input != "exit")
            //    {
            //        logMessage.Domain = $"{input}";

            //        Timelog.Client.Logger.Log(Microsoft.Extensions.Logging.LogLevel.Trace, logMessage);
            //    }
            //} while (input != "exit");
        }
    }
}
