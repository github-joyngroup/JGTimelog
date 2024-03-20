using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Timelog.Client;
using Timelog.Common.Models;

namespace Timelog.LogClientTester
{
    internal class Program
    {
        private const int nMessages = 1000;

        private static IConfiguration _configRoot;
        private static void Main(string[] args)
        {
            try
            {
                var configuration = ReadConfiguration("appsettings.json", Directory.GetCurrentDirectory());

                var applicationKey = _configRoot.GetValue<Guid>("ApplicationKey");
                var secondaryApplicationKey = _configRoot.GetValue<Guid>("SecondaryApplicationKey");

                InitializeApplication(applicationKey, configuration);
                RunApplication(nMessages);
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

        private static void InitializeApplication(Guid applicationKey, LoggerConfiguration configuration)
        {
            Logger.Startup(applicationKey, configuration, null);
        }

        /// <summary>
        /// Will generate and send log messages to the server
        /// The generated log messages will have random generated domains and transaction ids from a known pool of values
        /// </summary>
        private static void RunApplication(int nMessages)
        {
            Random r = new Random();
            int[] validDomains = new int[] { TimelogDomains.AssetGateway, TimelogDomains.AssetGatewayDokRouter, TimelogDomains.AssetGatewayDokRouterInitPipeline, TimelogDomains.AssetGatewayDokRouterTickPipeline };
            Guid[] validGuids = new Guid[] {    Guid.Parse("04f7ad43-b89a-48a9-aa4c-49877aef3a63"),
                                                Guid.Parse("d59e2dd7-7ab1-4a2d-98a8-b329b9bede1f"),
                                                Guid.Parse("76b9524c-eef4-400d-88a2-04b4c4b2d4fa"),
                                                Guid.Parse("3a0f9858-6efa-461e-8a6f-884ba9cb43c2"),
                                                Guid.Parse("aa123df7-c36d-4614-a2fe-653b003a3b7e") };


            double startProbability = 0.4;
            double stopProbability = 0.2;
            LogMessage startMessage = default(LogMessage);
            bool hasStartMessage = false;

            Console.WriteLine($"Start logging {nMessages} messages");
            int i = 0;
            while (i < nMessages)
            {
                if(!hasStartMessage && r.NextDouble() < startProbability)
                {
                    //Start operation
                    startMessage = Timelog.Client.Logger.LogStart(Microsoft.Extensions.Logging.LogLevel.Trace,
                            validDomains[r.Next(0, validDomains.Length)],
                            validGuids[r.Next(0, validGuids.Length)]);

                    hasStartMessage = true;
                }
                else if (hasStartMessage && r.NextDouble() < stopProbability)
                {
                    //Stop operation
                    Timelog.Client.Logger.LogStop(startMessage);
                    startMessage = default(LogMessage);
                    hasStartMessage = false;
                }
                else
                {
                    //Normal operation
                    Timelog.Client.Logger.Log(Microsoft.Extensions.Logging.LogLevel.Trace,
                            validDomains[r.Next(0, validDomains.Length)],
                            validGuids[r.Next(0, validGuids.Length)]);
                }
                
                i++;
                //Thread.Sleep(1000); // 1 / second
                Thread.Sleep(250); // 4 / second
                //Thread.Sleep(100);  // 10 / second
                //Thread.Sleep(10);  // 100 / second
                //Thread.Sleep(1);  // ~1000 / second
            }
        }

        /// <summary>
        /// Sends a lot of log messages to the server
        /// </summary>
        private static void RunApplication_OLD()
        {
            const int MAX = 10000000;

            Console.WriteLine($"Start logging {MAX} messages");
            int i = 0;
            while (true)
            {
                //convert the integer to binary and send it as the domain
                //logMessage.Domain = $"{Convert.ToString(i,2)}";
                Timelog.Client.Logger.Log(Microsoft.Extensions.Logging.LogLevel.Trace, TimelogDomains.AssetGatewayDokRouter, Guid.NewGuid(), null);
                //Timelog.Client.Logger.Log3();

                i++;
                //Thread.Sleep(1000); // 1 / second
                //Thread.Sleep(250); // 4 / second
                //Thread.Sleep(100);  // 10 / second
                //Thread.Sleep(10);  // 100 / second
                Thread.Sleep(1);  // ~1000 / second
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

        public static void MyMethod()
        {
            //a= Timelog.Client.Logger.LogStart(Microsoft.Extensions.Logging.LogLevel.Trace, new LogMessage(  ,,,,  ParameterizedThreadStartcommns.atartwatch,,));
            //Timelog.Client.Logger.LogStop(Microsoft.Extensions.Logging.LogLevel.Trace, a, stop);

        }

        public enum XPTO
        {
            None = 0,
            A = 1,
            B = 2,
            C = 3

        }
    }
}

