using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Timelog.Client;

namespace Timelog.LogClientTester
{
    internal class Program
    {
        private static IConfiguration _configRoot;
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

        public static LoggerConfiguration ReadConfiguration(string file, string filePath)
        {
            var configuration = new LoggerConfiguration();
            var configurationBuilder = new ConfigurationBuilder()
                .SetBasePath(filePath)
                .AddJsonFile(file);

            _configRoot = configurationBuilder.Build();
            _configRoot.GetSection("TimelogTestClientService").Bind(configuration);

            // Add additional validation if needed

            return configuration;
        }

        private static void InitializeApplication(LoggerConfiguration configuration)
        {
            Logger.Startup(configuration, null);
        }

        private static void RunApplication(LoggerConfiguration configuration)
        {
            var applicationKey = _configRoot.GetValue<Guid>("ApplicationKey");
            var secondaryApplicationKey = _configRoot.GetValue<Guid>("SecondaryApplicationKey");

            var logMessage = new Common.Models.LogMessage
            {
                ApplicationKey = applicationKey,
                Command = Common.Models.Commands.Start,
                Domain = Encoding.UTF8.GetBytes($"1234"),
                TransactionID = Guid.NewGuid(),
                OriginTimestamp = DateTime.UtcNow,
                Message = new Common.Models.Message { Header = $"", Data = Encoding.UTF8.GetBytes($"") },
            };

            const int MAX = 10000000;
            
            Console.WriteLine($"Start logging {MAX} messages");
            int i = 0;
            while (true)
            {
                //convert the integer to binary and send it as the domain
                //logMessage.Domain = $"{Convert.ToString(i,2)}";
                logMessage.Domain = Encoding.UTF8.GetBytes($"{i}");
                logMessage.ApplicationKey = applicationKey;
                if (i % 50000 == 0)
                {
                    //logMessage.ApplicationKey = secondaryApplicationKey;
                    Console.WriteLine($"Logging message {i}");
                }
                Timelog.Client.Logger.Log(Microsoft.Extensions.Logging.LogLevel.Trace, logMessage);
                //Timelog.Client.Logger.Log3();

                i++;
                //Thread.Sleep(1000);
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
