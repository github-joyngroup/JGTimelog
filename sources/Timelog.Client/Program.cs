using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace Timelog.Client
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            try
            {
                var configuration = Configuration.ReadConfiguration("appsettings.json");
                InitializeApplication(configuration);
                RunApplication();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during application initialization: {ex.Message}");
            }
        }

        private static void InitializeApplication(Configuration configuration)
        {
            ClientLogger.Init(configuration, null);
            // Other initialization logic if needed
        }

        private static void RunApplication()
        {
            var logMessage = new Common.Models.LogMessage
            {
                ApplicationKey = ClientLogger.ClientConfiguration.ApplicationKey,
                Command = Common.Models.Commands.Start,
                Domain = "",
                TransactionID = Guid.NewGuid(),
                Message = new Common.Models.Message { Header = $"", Data = Encoding.UTF8.GetBytes($"") },
            };

            const int MAX = 300;

            Console.WriteLine($"Start logging {MAX} messages");
            int i = 0;
            while (i < MAX)
            {
                logMessage.Domain = $"{i}";


                Timelog.Client.ClientLogger.Log(Microsoft.Extensions.Logging.LogLevel.Trace, logMessage);

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

                    Timelog.Client.ClientLogger.Log(Microsoft.Extensions.Logging.LogLevel.Trace, logMessage);
                }
            } while (input != "exit");
        }
    }
}
