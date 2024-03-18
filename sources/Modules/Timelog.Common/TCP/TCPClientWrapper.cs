using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Timelog.Common.Models;
using WatsonTcp;

namespace Timelog.Common.TCP
{
    /// <summary>
    /// Wraps a Watson TCP Client and exposes a simplified API to interact with it
    /// The configuration will include the host and port to connect to
    /// Will also include a mapping to a Method Info that should match the OnTimelogTCPOperationHandler delegate and will be invoked with
    /// contextualized information based on the operation being executed
    /// </summary>
    public class TCPClientWrapper
    {
        ///Private Properties 
        
        /// <summary>The unique application key to be used in the TCP communication</summary>
        private static Guid _applicationKey { get; set; }

        /// <summary>The configuration for the Watson TCP Client</summary>
        private static WatsonTcpClientConfiguration _configuration;

        /// <summary>TCP Server</summary>
        private static WatsonTcpClient _client;

        /// <summary>Logger to use</summary>
        private static ILogger _logger;

        /// <summary>Static JsonSerializerOptions to be reused</summary>
        private static JsonSerializerOptions includeFieldsJsonSerializerOptions = new JsonSerializerOptions { IncludeFields = true };

        //Public Events
        /// <summary>
        /// Will be triggered when a operation is received from the server
        /// </summary>
        public event OnTimelogTCPOperationHandler OnTimelogTCPOperation;


        #region Control Methods

        /// <summary>
        /// Setup the TCP Client based on the configuration. Will setup the Server host and port, and wire up the several TCP events
        /// </summary>
        public void Startup(Guid applicationKey, WatsonTcpClientConfiguration configuration, ILogger logger, OnTimelogTCPOperationHandler onTimelogTCPOperation)
        {
            _applicationKey = applicationKey;
            _configuration = configuration;
            _logger = logger;

            _client = new WatsonTcpClient(configuration.TimelogReportingHost, configuration.TimelogReportingPort);

            _client.Events.ServerConnected += OnTcpServerConnected;
            _client.Events.ServerDisconnected += OnTcpServerDisconnected;
            _client.Events.MessageReceived += OnTcpServerMessageReceived;
            _client.Settings.Guid = _applicationKey;

            OnTimelogTCPOperation += onTimelogTCPOperation;

            _logger?.LogInformation($"TCP Client '{_configuration.Name}...' started up. Will connect to {_configuration.TimelogReportingHost}:{_configuration.TimelogReportingPort}.");
        }

        /// <summary>
        /// Starts the TCP Client
        /// Will connect to the Timelog Reporting server and keep the connection alive
        /// When the connection is lost, it will retry to connect
        /// When the connection is established, it will send a Ping message to the server every CheckConnectionHealthFrequency, this will prevent server to disconnect the client
        /// </summary>
        public void Start(CancellationToken stoppingToken)
        {
            _logger?.LogInformation($"TCP Client '{_configuration.Name}...' is connecting...");
            Task.Run(() =>
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    if (!_client.Connected)
                    {
                        try
                        {
                            _client.Connect();
                            _logger?.LogInformation($"TCP Client '{_configuration.Name}...' connected...");
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogInformation($"TTCP Client '{_configuration.Name}...' Failed connection with message: {ex.Message}. Will retry");
                            Task.Delay(_configuration.RetryConnectFrequency, stoppingToken).Wait();
                        }
                    }
                    else
                    {
                        //Send Ping to server
                        SendMessage("Ping", new Dictionary<string, object>() { { Constants.TimelogTCPOperationKey, TimelogTCPOperation.Ping } });
                        Task.Delay(_configuration.CheckConnectionHealthFrequency, stoppingToken).Wait();
                    }
                }
            }, stoppingToken);
        }

        /// <summary>
        /// Stops the TCP Client and disconnects from the Server
        /// </summary>
        public void Stop()
        {
            _logger?.LogInformation($"TCP Client '{_configuration.Name}...' is disconnecting...");
            _client.Disconnect();
        }

        #endregion

        #region On TCP events

        /// <summary>
        /// Handles the connect to the server event
        /// </summary>
        private void OnTcpServerConnected(object sender, ConnectionEventArgs e)
        {
            _logger?.LogInformation($"TCP Client '{_configuration.Name}...' connected to {_configuration.TimelogReportingHost}:{_configuration.TimelogReportingPort}.");
            OnTimelogTCPOperation?.Invoke(TimelogTCPOperation.Connect, _applicationKey, null, null);
        }

        /// <summary>
        /// Handles the disconnect from the server event
        /// </summary>
        private void OnTcpServerDisconnected(object sender, DisconnectionEventArgs e)
        {
            _logger?.LogInformation($"TCP Client '{_configuration.Name}...' disconnected from {_configuration.TimelogReportingHost}:{_configuration.TimelogReportingPort}.");
            OnTimelogTCPOperation?.Invoke(TimelogTCPOperation.Connect, _applicationKey, null, null);
        }

        /// <summary>
        /// Handles a message received from the server
        /// Will check the message metadata for the operation and act accordingly
        /// </summary>
        private void OnTcpServerMessageReceived(object sender, MessageReceivedEventArgs e)
        {
            //Load the operation from the message metadata
            TimelogTCPOperation operation = TimelogTCPOperation.None;
            if (e.Metadata != null && e.Metadata.ContainsKey(Constants.TimelogTCPOperationKey))
            {
                operation = Enum.Parse<TimelogTCPOperation>(e.Metadata[Constants.TimelogTCPOperationKey].ToString());
            }

            //Based on the operation, act accordingly
            switch (operation)
            {
                //No operation, just log the message
                case TimelogTCPOperation.None:
                    _logger?.LogInformation($"Nop message from server at '{_configuration.TimelogReportingHost}:{_configuration.TimelogReportingPort}': '{Encoding.UTF8.GetString(e.Data)}'");
                    OnTimelogTCPOperation?.Invoke(operation, _applicationKey, null, null);
                    break;

                //Ping operation, just log the message
                case TimelogTCPOperation.Ping:
                    _logger?.LogInformation($"Ping message from server at '{_configuration.TimelogReportingHost}:{_configuration.TimelogReportingPort}': '{Encoding.UTF8.GetString(e.Data)}'");
                    OnTimelogTCPOperation?.Invoke(operation, _applicationKey, null, null);
                    break;

                //Current Filter parse the filter and log it
                case TimelogTCPOperation.CurrentFilter:
                    _logger?.LogInformation($"Receiving current filter from server at '{_configuration.TimelogReportingHost}:{_configuration.TimelogReportingPort}': '{Encoding.UTF8.GetString(e.Data)}'");
                    var filterStr = Encoding.UTF8.GetString(e.Data);
                    var filter = System.Text.Json.JsonSerializer.Deserialize<FilterCriteria>(filterStr);

                    //Do anything with the filter?
                    OnTimelogTCPOperation?.Invoke(operation, _applicationKey, new List<FilterCriteria>() { filter }, null);
                    break;

                //Received some log messages
                case TimelogTCPOperation.LogMessages:
                    var logMessagesStr = Encoding.UTF8.GetString(e.Data);
                    var logMessages = System.Text.Json.JsonSerializer.Deserialize<List<LogMessage>>(logMessagesStr, includeFieldsJsonSerializerOptions);

                    _logger?.LogInformation($"Receiving {logMessages.Count} log messages from server at '{_configuration.TimelogReportingHost}:{_configuration.TimelogReportingPort}'");

                    OnTimelogTCPOperation?.Invoke(operation, _applicationKey, null, logMessages);
                    break;

                //Fallback scenario, log the message
                default:
                    _logger?.LogWarning($"Message from server at '{_configuration.TimelogReportingHost}:{_configuration.TimelogReportingPort}': with unknown or unmapped operation: '{operation}'\r\n{Encoding.UTF8.GetString(e.Data)}");
                    break;
            }
        }

        #endregion

        #region Operations

        /// <summary>
        /// Sends a message to the Timelog Reporting server
        /// </summary>
        private void SendMessage(string message, Dictionary<string, object> metadata)
        {
            if (!_client.Connected) { throw new Exception("Client is not connected to Server. Correct and retry."); }
            _client.SendAsync(message, metadata);
        }

        /// <summary>
        /// Send message to the Timelog Reporting server to set a filter
        /// </summary>
        public void SetFilter(List<FilterCriteria> filterCriteria)
        {
            Dictionary<string, object> metadata = new Dictionary<string, object>()
            {
                { Constants.TimelogTCPOperationKey, TimelogTCPOperation.SetFilter }
            };
            SendMessage(System.Text.Json.JsonSerializer.Serialize(filterCriteria), metadata);
        }

        /// <summary>
        /// Send message to the Timelog Reporting server to get my current filter
        /// </summary>
        public void GetFilter()
        {
            Dictionary<string, object> metadata = new Dictionary<string, object>()
            {
                { Constants.TimelogTCPOperationKey, TimelogTCPOperation.GetFilter }
            };
            SendMessage("dummy", metadata);
        }

        #endregion
    }

    public class WatsonTcpClientConfiguration
    {
        /// <summary>
        /// The TCP Client Name to be used in logs
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The FQDN of the Timelog Reporting or their IP address
        /// </summary>
        public string TimelogReportingHost { get; set; }

        /// <summary>
        /// The Timelog reporting network port number
        /// </summary>
        public int TimelogReportingPort { get; set; }

        /// <summary>
        /// When connect fails, the amount of time to wait before retrying, default is 15000 = 15 seconds
        /// </summary>
        public int RetryConnectFrequency { get; set; } = 15000;

        /// <summary>
        /// When connected, the amount of time to wait before checking the connection health, default is 30000 = 30 seconds
        /// It's also the Ping Frequency when connection is established
        /// </summary>
        public int CheckConnectionHealthFrequency { get; set; } = 30000;
    }
}
