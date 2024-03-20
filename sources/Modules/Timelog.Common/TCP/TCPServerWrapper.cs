using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Timelog.Common.Models;
using WatsonTcp;

namespace Timelog.Common.TCP
{
    /// <summary>
    /// Wraps a Watson TCP Server and exposes a simplified API to interact with it
    /// The configuration will include the host and port to listen to, the authorized clients and the timeout for idle clients
    /// Will also include a mapping to a Method Info that should match the OnTimelogTCPOperationHandler delegate and will be invoked with
    /// contextualized information based on the operation being executed
    /// </summary>
    public class TCPServerWrapper
    {
        ///Private Properties 
        /// <summary>The configuration for the Watson TCP Server</summary>
        private WatsonTcpServerConfiguration _configuration;

        /// <summary>TCP Server</summary>
        private WatsonTcpServer _server;

        /// <summary>Logger to use</summary>
        private static ILogger _logger;

        //Public Events
        /// <summary>
        /// Will be triggered when a operation is received from a client
        /// </summary>
        public event OnTimelogTCPOperationHandler OnTimelogTCPOperation;

        /// <summary>
        /// Setup the TCP Server based on the configuration. Will setup the Server host and port, and wire up the several TCP events
        /// </summary>
        public void Startup(WatsonTcpServerConfiguration configuration, ILogger logger, OnTimelogTCPOperationHandler onTimelogTCPOperation)
        {
            _configuration = configuration;
            _logger = logger;

            _server = new WatsonTcpServer(_configuration.Host, _configuration.Port);

            _server.Events.ClientConnected += OnTcpClientConnected;
            _server.Events.ClientDisconnected += OnTcpClientDisconnected;
            _server.Events.MessageReceived += OnTcpClientMessageReceived;

            _server.Settings.IdleClientTimeoutSeconds = _configuration.IdleClientTimeoutSeconds;

            OnTimelogTCPOperation += onTimelogTCPOperation;

            _logger?.LogInformation($"TCP Server '{_configuration.Name}' setup. Will listen for clients in {_configuration.Host}:{_configuration.Port}.");
        }

        /// <summary>
        /// Starts the TCP Server
        /// </summary>
        public void Start()
        {
            _logger?.LogInformation($"TCP Server '{_configuration.Name}' is starting...");
            _server.Start();
            _logger?.LogDebug($"TCP Server '{_configuration.Name}' started.");
        }

        /// <summary>
        /// Stops the TCPServer
        /// </summary>
        public void Stop()
        {
            _logger?.LogInformation($"TCP Server '{_configuration.Name}' is stopping...");
            _server.Stop();
            _logger?.LogDebug($"TCP Server '{_configuration.Name}' stopped.");
        }

        /// <summary>
        /// Triggered when a new client connects to the server. Will check if the client is authorized to connect and log the event
        /// If the client is not authorized, will force a disconnect
        /// </summary>
        private void OnTcpClientConnected(object sender, ConnectionEventArgs e)
        {
            if (_configuration.AuthorizedAppKeys.Contains(e.Client.Guid))
            {
                _logger?.LogInformation($"TCP Server '{_configuration.Name}' got a new connection from {e.Client.ToString()}.");
                OnTimelogTCPOperation?.Invoke(TimelogTCPOperation.Connect, e.Client.Guid, null, null);
            }
            else
            {
                _logger?.LogInformation($"TCP Server '{_configuration.Name}' got a new connection from {e.Client.ToString()} but it's not authorized. Disconnecting...");
                _server.DisconnectClientAsync(e.Client.Guid);
            }
        }

        /// <summary>
        /// Disconnects a client from the server
        /// </summary>
        private void OnTcpClientDisconnected(object sender, DisconnectionEventArgs e)
        {
            _logger?.LogInformation($"TCP Server '{_configuration.Name}' client {e.Client.ToString()}' disconnected with reason {e.Reason}.");
            OnTimelogTCPOperation?.Invoke(TimelogTCPOperation.Disconnect, e.Client.Guid, null, null);
        }

        /// <summary>
        /// Handles a message received from a client
        /// Will check the message metadata for the operation and act accordingly
        /// </summary>
        private void OnTcpClientMessageReceived(object sender, MessageReceivedEventArgs e)
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
                    _logger?.LogDebug($"Nop message from {e.Client.ToString()}: '{Encoding.UTF8.GetString(e.Data)}'");
                    OnTimelogTCPOperation?.Invoke(operation, e.Client.Guid, null, null);
                    break;

                //Ping operation, just log the message
                case TimelogTCPOperation.Ping:
                    _logger?.LogDebug($"Ping message from {e.Client.ToString()}: '{Encoding.UTF8.GetString(e.Data)}'");
                    OnTimelogTCPOperation?.Invoke(operation, e.Client.Guid, null, null);
                    break;

                //Set Filter message, parse the filter and bubble up to OnTimeLogTCPOperation
                case TimelogTCPOperation.SetFilter:
                    _logger?.LogInformation($"{e.Client.ToString()} is setting it's filter");
                    var filter = ProtoBufSerializer.Deserialize<List<FilterCriteria>>(e.Data);

                    OnTimelogTCPOperation?.Invoke(operation, e.Client.Guid, filter, null);

                    _logger?.LogDebug($"{e.Client.ToString()} set it's filter");
                    break;

                //Get Filter message, bubble up to OnTimeLogTCPOperation
                case TimelogTCPOperation.GetFilter:
                    _logger?.LogInformation($"{e.Client.ToString()} is requesting it's current filter");
                    OnTimelogTCPOperation?.Invoke(operation, e.Client.Guid, null, null);
                    break;

                //Fallback scenario, log the message
                default:
                    _logger?.LogWarning($"Message from {e.Client.ToString()}: with unknown or unmapped operation: '{operation}'\r\n{Encoding.UTF8.GetString(e.Data)}");
                    break;
            }
        }

        /// <summary>
        /// Broadcasts a message to all clients
        /// </summary>
        public void BroadcastMessage(string message)
        {
            var metadata = new Dictionary<string, object>() { { Constants.TimelogTCPOperationKey, TimelogTCPOperation.None } };
            foreach (var client in _server.ListClients())
            {
                _server.SendAsync(client.Guid, message, metadata);
            }
        }

        /// <summary>
        /// Sends a message to a specific client, identified by it's guid
        /// </summary>
        public void SendMessage(Guid clientGuid, string message, Dictionary<string, object> metadata)
        {
            _server.SendAsync(clientGuid, message, metadata);
        }

        /// <summary>
        /// Sends the current filter to a specific client
        /// </summary>
        public void SendCurrentFilter(Guid clientGuid, List<FilterCriteria> filters)
        {
            _server.SendAsync(clientGuid, ProtoBufSerializer.Serialize(filters), new Dictionary<string, object>() { { Constants.TimelogTCPOperationKey, TimelogTCPOperation.CurrentFilter } });
        }

        /// <summary>
        /// Sends a list of log messages to a specific client
        /// </summary>
        public void SendLogMessages(Guid clientGuid, List<LogMessage> logMessages)
        {
            _server.SendAsync(clientGuid, ProtoBufSerializer.Serialize(logMessages), new Dictionary<string, object>() { { Constants.TimelogTCPOperationKey, TimelogTCPOperation.LogMessages } });
        }

        /// <summary>Returns all currenc connected clients</summary>
        public List<string> ListClients()
        {
            return _server.ListClients().Select(c => c.ToString()).ToList();
        }
    }

    public class WatsonTcpServerConfiguration
    {
        /// <summary>
        /// The TCP Server Name to be used in logs
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The host for clients to connect
        /// </summary>
        public string Host { get; set; }

        /// <summary>
        /// The listening port for clients to connect
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// The path of a json file with the Access Control List (ACL), of the clients that will be authorized to communicate with the server.
        /// </summary>
        public string AuthorizationsFilePath { get; set; }

        /// <summary>
        /// The list of authorized application keys
        /// </summary>
        public HashSet<Guid> AuthorizedAppKeys { get; set; }

        /// <summary>
        /// The amount of time in seconds that the server will wait for a client to send a message before disconnecting it
        /// </summary>
        public int IdleClientTimeoutSeconds { get; set; } = 60;
    }
}
