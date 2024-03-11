using System;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Timelog.Common.Models;
using Timelog.Common;
using Newtonsoft.Json;
using System.Diagnostics;

class LogViewer
{
    public static void Main()
    {
        //ListenerNamedPipesMessages();

        TestNamedPipesMessages();
    }

    public static void TestNamedPipesMessages()
    {
        int messageCount = 10000;  // Number of messages to send
        var stopwatch = new Stopwatch();  // Create a stopwatch to measure time

        using (var pipeServer = new NamedPipeServerStream("testpipe", PipeDirection.InOut))
        {
            try
            {
                Console.WriteLine("".Length.ToString());
                Console.WriteLine("Server waiting for client...");
                pipeServer.WaitForConnection();
                Console.WriteLine("Client connected.");

                var message = "";

                // Start the stopwatch
                stopwatch.Start();

                // Send the log message to the client
                using (StreamWriter writer = new StreamWriter(pipeServer, Encoding.UTF8, 256, true))
                {
                    // Send a large number of messages to the server
                    for (int i = 0; i < messageCount; i++)
                    {
                        writer.WriteLine(message);
                        writer.Flush();  // Ensure that the message is sent immediately
                    }
                }

                // Stop the stopwatch
                stopwatch.Stop();

                // Calculate the message rate
                double messagesPerSecond = messageCount / stopwatch.Elapsed.TotalSeconds;
                Console.WriteLine($"Sent {messageCount} messages in {stopwatch.Elapsed.TotalSeconds} seconds");
                Console.WriteLine($"Message rate: {messagesPerSecond} messages per second");

            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
                // Handle other exceptions as needed
            }
        }
    }

    public static void ListenerNamedPipesMessages()
    {
        using (var pipeServer = new NamedPipeServerStream("testpipe", PipeDirection.InOut))
        {
            try
            {
                Console.WriteLine("Server waiting for client...");
                pipeServer.WaitForConnection();
                Console.WriteLine("Client connected.");

                using (StreamReader reader = new StreamReader(pipeServer, Encoding.UTF8, false, 256, true))
                {
                    string message = reader.ReadLine();
                    Console.WriteLine("Received from client: " + message);
                }


                var logMessage = new Timelog.Common.Models.LogMessage
                {
                    ApplicationKey = Guid.Empty,
                    Command = Timelog.Common.Models.Commands.Start,
                    Domain = "Test",
                    TransactionID = Guid.NewGuid(),
                    OriginTimestamp = DateTime.UtcNow,
                    Message = new Timelog.Common.Models.Message { Header = "", Data = Encoding.UTF8.GetBytes("") },
                };

                // Serialize the log message to JSON
                string logString = JsonConvert.SerializeObject(logMessage);

                // Send the log message to the client
                using (StreamWriter writer = new StreamWriter(pipeServer, Encoding.UTF8, 256, true))
                {
                    writer.WriteLine(logString);
                    writer.Flush();  // Ensure that the message is sent immediately
                    Console.WriteLine("Sent log message to client: " + logString);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
                // Handle other exceptions as needed
            }
        }
    }

}