using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Timelog.Client;
using Timelog.Common.Models;

namespace Timelog.TestClientEfficiency
{
    public class Program
    {
        private static Dictionary<Guid, List<LogMessage>> applicationKeyGroupDictionary = new Dictionary<Guid, List<LogMessage>>();
        private static Dictionary<string, int> commandCountDictionary = new Dictionary<string, int>();
        private static int totalLogMessageCount = 0;
        private static int thresholdProcessingTime = 100; // Example threshold in milliseconds


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
                ApplicationKey = ClientLogger.GetClientConfiguration().ApplicationKey,
                Command = Common.Models.Commands.Start,
                TransactionID = Guid.NewGuid(),
                Message = new Common.Models.Message { Header = $"", Data = Encoding.UTF8.GetBytes($"") },
            };

            // Consider this table:
            // Instance || Time (ms) || Frequency
            // 1       || 1000  || 10
            // 2       || 250  || 40
            // 3       || 100  || 100
            // 4       || 50  || 200
            // 5       || 10  || 1000
            // 6       || 5  || 2000
            // 7       || 2  || 5000
            // 8       || 1  || 10000
            // 9       || 0  || 20000
            // I want a for for each instance, and inside the for, a while that logs the message the number of times specified in the frequency column.
            // The time column is the time in milliseconds that the while should wait before logging the next message.
            // The domain of each message should be the Instance Number + the number of the message in the while loop of frequencies
            // The message should be logged with the Trace level.
            // The while should be inside a for that iterates the number of times specified in the frequency column.

            for (int instance = 1; instance <= 9; instance++)
            {
                int frequency = GetFrequencyForInstance(instance);
                int timeInterval = GetTimeIntervalForInstance(instance);

                for (int messageNumber = 1; messageNumber <= frequency; messageNumber++)
                {
                    logMessage.Domain = $"{instance}_{messageNumber}";

                    ClientLogger.Log(Microsoft.Extensions.Logging.LogLevel.Trace, logMessage);

                    // Wait for the specified time interval
                    Thread.Sleep(timeInterval);
                }
            }


            PrintGraph();
        }


        // Function to get the frequency for each instance
        private static int GetFrequencyForInstance(int instance)
        {
            // This can be replaced with your actual logic or data source
            switch (instance)
            {
                case 1: return 10;
                case 2: return 40;
                case 3: return 100;
                case 4: return 200;
                case 5: return 1000;
                case 6: return 2000;
                case 7: return 5000;
                case 8: return 10000;
                case 9: return 20000;
                default: return 0;
            }
        }

        // Function to get the time interval for each instance in milliseconds
        private static int GetTimeIntervalForInstance(int instance)
        {
            // This can be replaced with your actual logic or data source
            switch (instance)
            {
                case 1: return 1000;
                case 2: return 250;
                case 3: return 100;
                case 4: return 50;
                case 5: return 10;
                case 6: return 5;
                case 7: return 2;
                case 8: return 1;
                case 9: return 0;
                default: return 0;
            }
        }


        public static void ProcessLogMessage(LogMessage message)
        {
            // 1. Count the Number of Log Messages
            totalLogMessageCount++;

            // 2. Grouping by ApplicationKey
            if (message.ApplicationKey != Guid.Empty)
            {
                if (!applicationKeyGroupDictionary.ContainsKey(message.ApplicationKey))
                {
                    applicationKeyGroupDictionary[message.ApplicationKey] = new List<LogMessage>();
                }

                applicationKeyGroupDictionary[message.ApplicationKey].Add(message);
            }

            // 3. Grouping by Command
            if (message.Command != null)
            {
                commandCountDictionary.TryGetValue(message.Command.ToString(), out int count);
                commandCountDictionary[message.Command.ToString()] = count + 1;
            }

            // 4. Time-based Analysis
            if (message.OriginTimestamp.HasValue)
            {
                // Calculate processing time (you may adjust this based on your needs)
                TimeSpan processingTime = DateTime.UtcNow - message.OriginTimestamp.Value;

                // Do further analysis or logging based on processing time
                if (processingTime.TotalMilliseconds > thresholdProcessingTime)
                {
                    Console.WriteLine($"High processing time detected: {processingTime.TotalMilliseconds} ms");
                }
            }

        }
        public static void PrintGraph()
        {
            Console.WriteLine("Command Frequency Graph:");

            foreach (var kvp in commandCountDictionary)
            {
                string commandGraph = new string('*', kvp.Value);
                Console.WriteLine($"{kvp.Key}: {commandGraph}");
            }
        }
    }
}
