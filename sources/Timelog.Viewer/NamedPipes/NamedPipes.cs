using System;
using System.IO.Pipes;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using Confluent.Kafka;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace Timelog.Viewer.NamedPipes
{
    public class NamedPipes
    {
        public static void SendMessage()
        {
            using (NamedPipeClientStream pipeClient = new NamedPipeClientStream(".", "testpipe", PipeDirection.InOut))
            {
                try
                {
                    Console.WriteLine("Client waiting for the server...");
                    pipeClient.Connect();
                    Console.WriteLine("Connected to server.");

                    using (StreamWriter writer = new StreamWriter(pipeClient, Encoding.UTF8, 256, true))
                    {
                        writer.WriteLine("I want the the real time logs!");
                        writer.Flush();  // Ensure that the message is sent immediately
                        Console.WriteLine("Sent to server: I want the the real time logs!");
                    }

                    // Wait for the server to respond
                    pipeClient.WaitForPipeDrain();

                    using (StreamReader reader = new StreamReader(pipeClient, Encoding.UTF8, false, 256, true))
                    {
                        string message = reader.ReadLine();
                        Console.WriteLine("Received from server: " + message);
                    }

                    0.ToString();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error: " + ex.Message);
                    // Handle other exceptions as needed
                }
            }
        }

        public static void TestNamedPipesMessages()
        {
            using (NamedPipeClientStream pipeClient = new NamedPipeClientStream(".", "testpipe", PipeDirection.InOut))
            {
                // Receive and process a large number of messages from the client
                int messageCount = 10000;  // Expected number of messages from the client
                int messagesReceived = 0;  // Counter for received messages

                try
                {
                    Console.WriteLine("Client waiting for the server...");
                    pipeClient.Connect();
                    Console.WriteLine("Connected to server.");

                    using (StreamReader reader = new StreamReader(pipeClient, Encoding.UTF8, false, 256, true))
                    {
                        while (messagesReceived < messageCount)
                        {
                            string message = reader.ReadLine();
                            Console.WriteLine("Received from client: " + message);
                            // Process the received message as needed
                            messagesReceived++;
                        }
                    }

                    Console.WriteLine($"Received {messagesReceived} messages from the client");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error: " + ex.Message);
                    // Handle other exceptions as needed
                }
            }
        }

    }
}