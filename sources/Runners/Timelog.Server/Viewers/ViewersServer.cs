using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Timelog.Common.Models;
using Timelog.Common.TCP;

namespace Timelog.Server.Viewers
{
    /// <summary>
    /// Will handle the viewers and their filters
    /// Will expose a TCP connection for the viewers to communicate with the server
    /// Will hold the Array of current viewers, this array indexes the static array of viewers and filters
    /// </summary>
    internal class ViewersServer : BackgroundService
    {

        //Internal Members
        /// <summary>
        /// Master index of Current viewers, each entry will point to the index of the viewer in the static array of viewers
        /// </summary>
        internal static HashSet<int> CurrentViewersIndexes;

        /// <summary>
        /// This has to be internal to allow the LogServerClient to access the current viewers
        /// I'm not entirelly satistied with the usage of a lock and a try/catch, must study for better alternatives
        /// </summary>
        internal static LogViewer[] LogViewers 
        {
            get
            {
                _logViewerLocker.EnterReadLock();
                try
                {
                    return _logViewers[_logViewerCurrentIndex];
                }
                finally
                {
                    _logViewerLocker.ExitReadLock();
                }
            }
        }

        //Private Members

        /// <summary>Logger to use</summary>
        private static ILogger _logger;

        /// <summary>
        /// The configuration of the module
        /// </summary>
        private static ViewersServerConfiguration _configuration;

        /// <summary>
        /// Jagged array, the first index allows toggle between active-passive to allow hot swapping of the viewers
        /// Each entry will be a LogViewer, this array will be loaded on the startup of the server with the maximum allowed number of entries
        /// </summary>
        public static LogViewer[][] _logViewers;

        /// <summary>
        /// Current index of the first dimension of the jagged array _logViewers - will have the value 0 or 1 and will allow hot swapping of the viewers
        /// </summary>
        private static int _logViewerCurrentIndex;

        /// <summary>
        /// Prevents concurrent write access to the _logViewers array
        /// </summary>
        private static ReaderWriterLockSlim _logViewerLocker = new ReaderWriterLockSlim();

        /// <summary>
        /// TCP Connections Handler
        /// </summary>
        internal static TCPServerWrapper _server;

        /// <summary>
        /// Setup the Views Manager, load configuration, initializes _logViewers and prepare the TCP socket to start listening
        /// </summary>
        public static void Startup(ViewersServerConfiguration configuration, ILogger logger)
        {
            _configuration = configuration;
            _logger = logger;

            LoadViewers();

            _configuration.WatsonTcpServerConfiguration.AuthorizedAppKeys = GetAuthorizedAppKeys();
            _server = new TCPServerWrapper();
            _server.Startup(_configuration.WatsonTcpServerConfiguration, logger, OnTimelogTCPOperation);

            _logger?.LogInformation($"Viewers Manager setup.");
        }


        /// <summary>
        /// Starts the ViewerServer
        /// </summary>
        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger?.LogInformation($"Viewers manager is starting...");
            return Task.Run(() => _server.Start(), stoppingToken);
        }

        /// <summary>
        /// Stops the ViewerServer
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger?.LogInformation($"Viewers manager is stopping...");
            await Task.Run(() => _server.Stop(), cancellationToken);
        }

        /// <summary>
        /// Handles a TCP operation. As the TCP communication is handled by the TCPServerWrapper this class shall only handle business logic
        /// </summary>
        private static void OnTimelogTCPOperation(TimelogTCPOperation operation, Guid clientGuid, List<FilterCriteria> filters)
        {
            switch (operation)
            {

                case TimelogTCPOperation.Disconnect:
                    RemoveFilters(clientGuid);
                    break;

                case TimelogTCPOperation.SetFilter:
                    //If filters are present, will add them to the viewer, if not will remove the filters
                    //This way the same SetFilter operation can be used to add or remove filters by the client
                    if (filters != null && filters.Any())
                    {
                        AddFilters(clientGuid, filters);
                    }
                    else
                    {
                        RemoveFilters(clientGuid);
                    }
                    break;

                case TimelogTCPOperation.GetFilter:
                    var currentFilters = GetFilters(clientGuid);
                    _server.SendMessage(clientGuid, System.Text.Json.JsonSerializer.Serialize(currentFilters), new Dictionary<string, object>() { { Constants.TimelogTCPOperationKey, TimelogTCPOperation.CurrentFilter } });
                    break;

                default:
                    _logger?.LogDebug($"Operation not implemented: {operation.ToString()} - Client: '{clientGuid.ToString()}'");
                    break;
            }
        }

        //Private methods

        #region Viewers Management Methods

        /// <summary>
        /// Load the authorized clients from the path defined configuration file, 
        /// if path or file not present, will load directly from the configuration
        /// </summary>
        private static void LoadViewers()
        {
            var baseAcceptedApplicationKeys = new List<Guid>();

            try
            {
                if (!string.IsNullOrEmpty(_configuration?.AuthorizedAppKeysFilePath) && File.Exists(_configuration.AuthorizedAppKeysFilePath))
                {
                    var lines = File.ReadAllLines(_configuration.AuthorizedAppKeysFilePath);

                    foreach (var line in lines)
                    {
                        Guid candidateGuid = Guid.Empty;
                        if (Guid.TryParse(line, out candidateGuid))
                        {
                            baseAcceptedApplicationKeys.Add(candidateGuid);
                        }
                    }
                }
                else if (_configuration?.AuthorizedAppKeysDirect is not null)
                {
                    baseAcceptedApplicationKeys = _configuration.AuthorizedAppKeysDirect;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error loading authorized clients: {ex.Message}");
            }

            if (baseAcceptedApplicationKeys is null || baseAcceptedApplicationKeys.Count == 0)
            {
                var errorMsg = $"No authorized clients found. Please check the configuration file or the authorized apps file.";
                _logger.LogError(errorMsg);
                throw new InvalidOperationException(errorMsg);
            }
            if (baseAcceptedApplicationKeys.Count > _configuration.MaximumNumberViewers)
            {
                var errorMsg = $"Found {baseAcceptedApplicationKeys.Count} but system only allows {_configuration.MaximumNumberViewers}. Please check the configuration file or the authorized apps file.";
                _logger.LogError(errorMsg);
                throw new InvalidOperationException(errorMsg);
            }

            _logViewerLocker.EnterWriteLock();
            try
            {
                _logViewerCurrentIndex = 0;
                _logViewers = new LogViewer[2][];
                _logViewers[0] = baseAcceptedApplicationKeys.Select((bapk, idx) => new LogViewer(bapk, idx)).ToArray();
                _logViewers[1] = new LogViewer[baseAcceptedApplicationKeys.Count];

                CurrentViewersIndexes = new HashSet<int>();
            }
            finally
            {
                _logViewerLocker.ExitWriteLock();
            }
        }

        /// <summary>
        /// Will Find the index of the viewerin the current viewers list
        /// </summary>
        private static int FindViewerIndex(Guid viewerGuid)
        {
            _logViewerLocker.EnterReadLock();
            try
            {
                return _logViewers[_logViewerCurrentIndex].ToList().FindIndex(x => x.ApplicationKey == viewerGuid);
            }
            finally
            {
                _logViewerLocker.ExitReadLock();
            }
        }

        /// <summary>
        /// Will add the filters for the viewer identified by the viewerGuid 
        /// </summary>
        /// <param name="viewerGuid"></param>
        /// <param name="filters"></param>
        private static void AddFilters(Guid viewerGuid, List<FilterCriteria> filters)
        {
            var viewerIdx = FindViewerIndex(viewerGuid); 
            
            var nextIndex = _logViewerCurrentIndex == 0 ? 1 : 0;
            
            _logViewerLocker.EnterReadLock();
            try
            {
                //Copy current to next
                _logViewers[_logViewerCurrentIndex].CopyTo(_logViewers[nextIndex],0);
            }
            finally
            {
                _logViewerLocker.ExitReadLock();
            }

            //Update the client guid filters
            _logViewers[nextIndex][viewerIdx].Filters = filters;
            _logViewerLocker.EnterWriteLock();
            try
            {
                _logViewerCurrentIndex = nextIndex;
                CurrentViewersIndexes.Add(viewerIdx);
            }
            finally
            {
                _logViewerLocker.ExitWriteLock();
            }
        }

        public static void RemoveFilters(Guid viewerGuid)
        {
            var viewerIdx = FindViewerIndex(viewerGuid);
            var nextIndex = _logViewerCurrentIndex == 0 ? 1 : 0;

            _logViewerLocker.EnterReadLock();
            try
            {
                //Copy current to next
                _logViewers[_logViewerCurrentIndex].CopyTo(_logViewers[nextIndex], 0);
            }
            finally
            {
                _logViewerLocker.ExitReadLock();
            }

            //Update the client guid filters
            _logViewers[nextIndex][viewerIdx].Filters = null;
            _logViewerLocker.EnterWriteLock();
            try
            {
                _logViewerCurrentIndex = nextIndex;
                CurrentViewersIndexes.Remove(viewerIdx);
            }
            finally
            {
                _logViewerLocker.ExitWriteLock();
            }
        }

        public static List<FilterCriteria> GetFilters(Guid viewerGuid)
        {
            var viewerIdx = FindViewerIndex(viewerGuid);
            return _logViewers[_logViewerCurrentIndex][viewerIdx].Filters;
        }

        #endregion

        //Public Methods

        /// <summary>
        /// Returns the full list of Authorized App Keys
        /// </summary>
        /// <returns></returns>
        public static HashSet<Guid> GetAuthorizedAppKeys()
        {
            _logViewerLocker.EnterReadLock();
            try
            {
                return _logViewers[_logViewerCurrentIndex].Select(x => x.ApplicationKey).ToHashSet();
            }
            finally
            {
                _logViewerLocker.ExitReadLock();
            }


        }
    }

    public class ViewersServerConfiguration
    {
        /// <summary>
        /// Maximum number of Viewers - if configuration is greater than this value system will not start
        /// </summary>
        public int MaximumNumberViewers { get; set; } = 32;

        /// <summary>
        /// The path of a json file with the Access Control List (ACL), of the clients that will be authorized to communicate with the server.
        /// </summary>
        public string AuthorizedAppKeysFilePath { get; set; }

        /// <summary>
        /// Other way to configure the list of Guids that can communicate with the server.
        /// </summary>
        public List<Guid> AuthorizedAppKeysDirect { get; set; }

        public WatsonTcpServerConfiguration WatsonTcpServerConfiguration { get; set; }
    }
}
