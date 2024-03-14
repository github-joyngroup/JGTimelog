using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Net.Sockets;
using System.Text;
using Timelog.Common;
using Timelog.Common.Models;

namespace Timelog.ClientReport
{
    public static class ClientReportLogger
    {
        private static HubConnection _hubConnection;
        private static ILogger _logger;
        private static bool isInitialized = false;

        public static async Task Init(string hubUrl, ILogger logger)
        {
            if (isInitialized)
            {
                throw new InvalidOperationException("Logger has already been initialized.");
            }

            _logger = logger;

            _hubConnection = new HubConnectionBuilder()
                .WithAutomaticReconnect() // Enable automatic reconnection
                .Build();

            _hubConnection.On<string>("ReceiveLogMessage", (logMessage) =>
            {
                // Process the received log message
                // You can add your logic to handle the log message here
            });

            _hubConnection.Closed += async (error) =>
            {
                // Handle reconnection if the connection is closed
                await Task.Delay(new Random().Next(0, 5) * 1000);
                await _hubConnection.StartAsync();
            };

            try
            {
                await _hubConnection.StartAsync();
                _logger?.LogInformation("ClientReportLogger is connected to the SignalR hub.");
                isInitialized = true;
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error connecting to the SignalR hub: {ex.Message}");
                throw;
            }
        }

        public static async Task Log(LogMessage message)
        {
            string logString = JsonConvert.SerializeObject(message);

            try
            {
                await _hubConnection.SendAsync("SendLogMessage", logString);
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error sending log message to SignalR hub: {ex.Message}");
            }
        }
    }
}