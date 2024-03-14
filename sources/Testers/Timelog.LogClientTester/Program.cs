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
            Logger.Startup(configuration, null);
        }

        private static void RunApplication(Configuration configuration)
        {
            var logMessage = new Common.Models.LogMessage
            {
                ApplicationKey = configuration.ApplicationKey,
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
                logMessage.ApplicationKey = Guid.Parse("8ce94d5e-b2a3-4685-9e6c-ab21410b595f");
                if (i % 15000 == 0)
                {
                    logMessage.ApplicationKey = Guid.Parse("43e719fd-62bc-441f-80ba-cbb2a92ba44c");
                    Console.WriteLine($"Logging message {i}");
                }
                Timelog.Client.Logger.Log(Microsoft.Extensions.Logging.LogLevel.Trace, logMessage);
                //Timelog.Client.Logger.Log3();

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
