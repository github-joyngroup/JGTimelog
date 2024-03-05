using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Timelog.Tests
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var configuration = new Timelog.Client.Configuration
            {
                ApplicationKey = Guid.Parse("8ce94d5e-b2a3-4685-9e6c-ab21410b595f"),
                TimelogServerHost = "localhost",
                TimelogServerPort = 7777,
            };

            Timelog.Client.Logger.Startup(configuration, null);


            var logMessage = new Timelog.Common.Models.LogMessage
            {
                ApplicationKey = configuration.ApplicationKey,
                Command = Common.Models.Commands.Start,
                Domain = "",
                TransactionID = Guid.NewGuid(),
                OriginTimestamp = DateTime.UtcNow,
                Message = new Common.Models.Message { Header = $"", Data = Encoding.UTF8.GetBytes($"") },
            };

            const int MAX = 300;
            
            Console.WriteLine($"Start logging {MAX} messages");
            int i = 0;
            while (i < MAX)
            {
                logMessage.Domain = $"{i}";


                Timelog.Client.Logger.Log(Microsoft.Extensions.Logging.LogLevel.Trace, logMessage);
                
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

                    Timelog.Client.Logger.Log(Microsoft.Extensions.Logging.LogLevel.Trace, logMessage);
                }
            } while (input != "exit");
        }
    }
}
