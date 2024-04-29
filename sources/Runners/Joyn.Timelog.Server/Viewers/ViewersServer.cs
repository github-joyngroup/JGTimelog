using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Joyn.Timelog.Common;
using Joyn.Timelog.Common.Models;
using Joyn.Timelog.Common.TCP;

namespace Joyn.Timelog.Server.Viewers
{
    /// <summary>
    /// Will handle the viewers and their filters
    /// Will expose a TCP connection for the viewers to communicate with the server
    /// Will hold the Array of current viewers, this array indexes the static array of viewers and filters
    /// </summary>
    internal class ViewersServer : BackgroundService
    {
        //Internal Properties

        /// <summary>
        /// This has to be internal so that main Listening Thread can access it and decide to reload the viewers
        /// </summary>
        internal static bool LogViewersDirty { get; set; } = false;

        /// <summary>Current File Dump index</summary>
        internal static int LastDumpToViewersIndex { get; set; }

        /// <summary>Getter for the Flush Items Size configuration</summary>
        internal static int FlushItemsSize { get { return _configuration.FlushItemsSize; } }

        //Private Members

        /// <summary>
        /// Master index of Current viewers, each entry will point to the index of the viewer in the static array of viewers
        /// </summary>
        private static HashSet<int> _currentViewersIndexes;

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
        private static LogViewer[][] _logViewers;

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
        private static TCPServerWrapper _server;

        /// <summary>
        /// Round robin array to store the log messages as they are received
        /// </summary>
        private static RoundRobinArray<LogMessage>? _receivedDataQueue;

        /// <summary>The cancellation token source to stop the listener - will be flagged on the stop method</summary>
        private static CancellationTokenSource StoppingCancelationTokenSource;

        /// <summary>
        /// Lock object to synchronize access to the flushing thread
        /// </summary>
        private static object _flushingThreadLock = new();

        //Methods

        /// <summary>
        /// Setup the Views Manager, load configuration, initializes _logViewers and prepare the TCP socket to start listening
        /// </summary>
        public static void Startup(ViewersServerConfiguration configuration, RoundRobinArray<LogMessage>? receivedDataQueue, ILogger logger)
        {
            _configuration = configuration;
            _logger = logger;

            LoadViewers();

            _configuration.WatsonTcpServerConfiguration.AuthorizedAppKeys = GetAuthorizedAppKeys();
            _server = new TCPServerWrapper();
            _server.Startup(_configuration.WatsonTcpServerConfiguration, logger, OnTimelogTCPOperation);

            _receivedDataQueue = receivedDataQueue;

            _logger?.LogInformation($"Viewers Manager setup.");
        }

        /// <summary>
        /// Starts the ViewerServer
        /// </summary>
        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            StoppingCancelationTokenSource = new CancellationTokenSource();

            _logger?.LogInformation($"Viewers manager is starting...");
            return Task.Run(() =>
            {
                _server.Start();
                FlushingThread(StoppingCancelationTokenSource.Token);
            }, stoppingToken);
        }

        /// <summary>
        /// Stops the ViewerServer
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger?.LogInformation($"Viewers manager is stopping...");

            StoppingCancelationTokenSource.Cancel();

            await Task.Run(() =>
            {
                _server.Stop();
            }, cancellationToken);
        }

        /// <summary>
        /// Handles a TCP operation. As the TCP communication is handled by the TCPServerWrapper this class shall only handle business logic
        /// </summary>
        private static void OnTimelogTCPOperation(TimelogTCPOperation operation, Guid clientGuid, List<FilterCriteria> filters, List<LogMessage> logMessages)
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
                    _server.SendCurrentFilter(clientGuid, currentFilters);
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

                _currentViewersIndexes = new HashSet<int>();
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
                _currentViewersIndexes.Add(viewerIdx);
                LogViewersDirty = true;
            }
            finally
            {
                _logViewerLocker.ExitWriteLock();
            }
        }

        private static void RemoveFilters(Guid viewerGuid)
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
                _currentViewersIndexes.Remove(viewerIdx);
                LogViewersDirty = true;
            }
            finally
            {
                _logViewerLocker.ExitWriteLock();
            }
        }

        private static List<FilterCriteria> GetFilters(Guid viewerGuid)
        {
            var viewerIdx = FindViewerIndex(viewerGuid);
            return _logViewers[_logViewerCurrentIndex][viewerIdx].Filters;
        }
        
        /// <summary>
        /// Returns the full list of Authorized App Keys
        /// </summary>
        /// <returns></returns>
        private static HashSet<Guid> GetAuthorizedAppKeys()
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

        #endregion

        //Public Methods
        public static List<LogViewer> CloneCurrentLogViewers()
        {
            _logViewerLocker.EnterReadLock();
            try
            {
                return _logViewers[_logViewerCurrentIndex].ToList();
            }
            finally
            {
                LogViewersDirty = false;
                _logViewerLocker.ExitReadLock();
            }
        }

        #region Main Flushing Thread

        public static void Pulse()
        {
            lock (_flushingThreadLock)
            {
                Monitor.Pulse(_flushingThreadLock);
            }
        }

        /// <summary>
        /// The main flushing thread that periodically flushes the log files
        /// </summary>
        private static void FlushingThread(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                lock (_flushingThreadLock)
                {
                    _logger.LogDebug("LogFileManager FlushingThread waked up"); //Might produce too many logs without interest
                    SendLogsToViewers();
                    Monitor.Wait(_flushingThreadLock, _configuration.FlushTimeSeconds * 1000);
                }
            }
        }

        /// <summary>
        /// This method is used to send logs to viewers periodically.
        /// It calculates the fromIndex by taking the modulus of lastIndexDumpedtofile and the length of ReceivedDataQueue items.
        /// If the currentIndex is greater than or equal to fromIndex, it means we are still in the same round robin cycle.
        /// In this case, it loads the new entries from fromIndex to currentIndex.
        /// If the currentIndex is less than fromIndex, it means we are in the next round robin cycle.
        /// In this case, it loads the new entries to the log file from fromIndex to the end and from the start to currentIndex.
        /// Aftewards, each log message will be placed on a package for each report viewer that has the corresponding bit set in the filter bitmask.
        /// Finnally a server TCP message is sent to each log viewer with the package of log messages that are relevant to them.
        /// </summary>
        /// <param name="currentIndex">The current index in the round robin cycle.</param>
        /// <param name="lastIndexDumpedtofile">The last index that was dumped to the file.</param>
        private static void SendLogsToViewers()
        {
            var queueSnapshot = _receivedDataQueue.GetSnapshot();

            int currentIndex = queueSnapshot.CurrentIndex;
            int fromIndex = LastDumpToViewersIndex % queueSnapshot.LogMessages.Length;            

            if(fromIndex == currentIndex) 
            {
                //Index did not change, no new messages
                return; 
            }

            //If no valid messages
            if (!queueSnapshot.LogMessages.Any(clm => clm.ApplicationKey != Guid.Empty))
            {
                LastDumpToViewersIndex = currentIndex;
                return;
            }

            _logger.LogInformation($"Will send to viewers from Index {fromIndex} to {currentIndex}");

            List<LogMessage> logsToSend = new List<LogMessage>(); 
            if (currentIndex >= fromIndex)
            {
                // still in the same round robin cycle

                //dump the new  entries to the log file
                logsToSend.AddRange(queueSnapshot.LogMessages.ToArray()[fromIndex..currentIndex]);
            }
            else if (currentIndex < fromIndex)
            {
                // we are in the round robin cycle

                //dump the new  entries to the log file
                logsToSend.AddRange(queueSnapshot.LogMessages.ToArray()[fromIndex..]);
                logsToSend.AddRange(queueSnapshot.LogMessages.ToArray()[0..currentIndex]);
            }

            Dictionary<Guid, List<LogMessage>> logsToSendByViewer = new Dictionary<Guid, List<LogMessage>>();
            foreach(var logMessage in logsToSend)
            {
                var reportViewers = GetReportViewersForBitmask(logMessage.FilterBitmask);
                foreach(var reportViewer in reportViewers)
                {
                    if (!logsToSendByViewer.ContainsKey(reportViewer))
                    {
                        logsToSendByViewer[reportViewer] = new List<LogMessage>();
                    }
                    logsToSendByViewer[reportViewer].Add(logMessage);
                }
            }

            foreach(var viewer in logsToSendByViewer)
            {
                _server.SendLogMessages(viewer.Key, viewer.Value);
            }

            //Update the last dump to file index with the current index
            LastDumpToViewersIndex = currentIndex;
        }

        private static Dictionary<long, List<Guid>> BitSetPositionsCache = new Dictionary<long, List<Guid>>();
        private static List<Guid> GetReportViewersForBitmask(long originalBitmask)
        {
            if (BitSetPositionsCache.ContainsKey(originalBitmask)) { return BitSetPositionsCache[originalBitmask];}

            List<Guid> viewers = new List<Guid>();
            int position = 0; //Start from the least significant bit (LSB)
            long bitmask = originalBitmask;
            while(bitmask > 0)
            {
                if((bitmask & 1)== 1) //Check it the LSB is set
                {
                    viewers.Add(_logViewers[0][position].ApplicationKey);
                }
                position++;
                bitmask >>= 1; //Shift the bits to the right
            }
            BitSetPositionsCache[originalBitmask] = viewers;
            return BitSetPositionsCache[originalBitmask];
        }

        #endregion
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

        /// <summary>
        /// Frequency to flush the log entries back to the viewers
        /// </summary>
        public int FlushTimeSeconds { get; set; } = 1;

        /// <summary>
        /// The number of entries that force a flush to the viewers. Default is 5000.
        /// </summary>
        public int FlushItemsSize { get; set; } = 5000;
    }
}
