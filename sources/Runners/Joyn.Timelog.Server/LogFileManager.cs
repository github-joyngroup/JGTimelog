using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Joyn.Timelog.Common;
using Joyn.Timelog.Common.Models;
using Joyn.Timelog.Common.TCP;
using Joyn.Timelog.Server.Viewers;

namespace Joyn.Timelog.Server
{
    /// <summary>
    /// Responsible for managing the log files, including writing to the log files and rotating the log files
    /// </summary>
    internal class LogFileManager : BackgroundService
    {
        //Private fields
        /// <summary>Logger to use</summary>
        private static ILogger _logger;

        /// <summary>Configuration to use</summary>
        private static LogFileManagerConfiguration _configuration;

        /// <summary>
        /// Private field to store the current file index
        /// </summary>
        private static int _currentFileIndex;

        /// <summary>
        /// Count the number of entries dumped to the log file
        /// </summary>
        private static int _entriesDumpedToFile;

        ///<summary>
        ///the last read index from the ReceivedDataQueue
        ///</summary>
        private static (int roundRobinCounter, int lastReadIndex) _lastQueueRead;

        /// <summary>
        /// Base path fot the log files
        /// </summary>
        private static string _logFilesPath;

        /// <summary>
        /// Max number of files to write
        /// </summary>
        private static int _maxFiles;

        /// <summary>
        /// Max number of entries per file
        /// </summary>
        private static int _maxEntriesPerFile;

        /// <summary>
        /// Lock object to synchronize access to the flushing thread
        /// </summary>
        private static object _flushingThreadLock = new();

        /// <summary>
        /// Round robin array to store the log messages as they are received
        /// </summary>
        private static RoundRobinArray<LogMessage>? _receivedDataQueue;

        /// <summary>
        /// Stream writer to write to the log file
        /// </summary>
        private static FileStream? _writer;

        /// <summary>The cancellation token source to stop the listener - will be flagged on the stop method</summary>
        private static CancellationTokenSource StoppingCancelationTokenSource;

        //public Properties 
        /// <summary>Current File Dump index</summary>
        internal static int LastDumpToFileIndex { get; set; }

        /// <summary>Getter for the Flush Items Size configuration</summary>
        internal static int FlushItemsSize { get { return _configuration.FlushItemsSize; } }

        /// <summary>
        /// Setup the Log File Manager, load configuration, initialize directory, and load static variables
        /// </summary>
        public static void Startup(LogFileManagerConfiguration configuration, RoundRobinArray<LogMessage>? receivedDataQueue, ILogger logger)
        {
            _configuration = configuration;
            _logger = logger;

            _logFilesPath = _configuration.LogFilesPath;
            _maxFiles = _configuration.MaxLogFiles;
            _maxEntriesPerFile = _configuration.MaxLogFileEntries;

            if (!Directory.Exists(_logFilesPath))
            {
                //EPocas, 2024-03-11, create directory if it does not exist
                Directory.CreateDirectory(_logFilesPath);
                //throw new DirectoryNotFoundException($"The directory '{logFilesPath}' does not exist.");
            }
            _receivedDataQueue = receivedDataQueue;
            
            var _lastFile = GetLastFilePath(_logFilesPath);
            _currentFileIndex = GetFileIndex(_lastFile);
            _entriesDumpedToFile = GetEntriesDumpedToFiles(_lastFile);
            _lastQueueRead.lastReadIndex = 0;
            _lastQueueRead.roundRobinCounter = 0;            

            _logger?.LogInformation($"LogFile Manager setup.");
        }

        /// <summary>
        /// Starts the LogFileManager thread
        /// </summary>
        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger?.LogInformation($"LogFileManager is starting...");

            StoppingCancelationTokenSource = new CancellationTokenSource();

            //Start listening to the UDP port
            new Thread(() =>
            {
                OpenNextStreamWriter();
                FlushingThread(StoppingCancelationTokenSource.Token);
            }).Start();

            return Task.CompletedTask;
        }

        /// <summary>
        /// Stops the UDPListener and the LogFileManager thread
        /// </summary>
        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger?.LogInformation($"LogFileManager is stopping.");

            StoppingCancelationTokenSource.Cancel();
            await Task.Run(() =>
            { 
                LogFileManager.DumpLogToFiles();
                LogFileManager.Close();
            }, CancellationToken.None);

            await base.StopAsync(StoppingCancelationTokenSource.Token);
        }

        #region Private support methods

        /// <summary>
        /// Find the last written file in the directory
        /// and get the file with most recent date
        /// </summary>
        private static FileInfo GetLastFilePath(string fileDirectoryPath)
        {
            var directory = new DirectoryInfo(fileDirectoryPath);
            var files = directory.GetFiles();

            if (files.Length == 0) { return new FileInfo(GetFilePath()); }

            var myFile = (from f in files
                          orderby f.LastWriteTime descending
                          select f).First();

            return myFile;
        }

        /// <summary>
        /// Get the file path for the current log file
        /// </summary>
        /// <returns></returns>
        private static string GetFilePath()
        {
            string filePath = string.Format(Path.Combine(_logFilesPath, "Timelog_{0}.txt"), _currentFileIndex.ToString().PadLeft(5, '0'));
            return filePath;
        }

        /// <summary>
        /// Opens a stream writter to write to the log file
        /// </summary>  
        private static FileStream OpenFileStream(bool fileChanged)
        {
            string filePath = GetFilePath();
            //return new StreamWriter(filePath, append: !fileChanged);
            return new FileStream(filePath, fileChanged ? FileMode.Create : FileMode.Append, FileAccess.Write, FileShare.Read);
        }

        /// <summary>
        /// Opens the next stream writer to write to the log file
        /// </summary>
        private static void OpenNextStreamWriter()
        {
            if (_writer != null)
            {
                _writer.Dispose();
            }
            bool fileChanged = false;
            //currentFileIndex = GetFileIndex(GetLastFilePath(_logFilesPath));
            if (RoundRobinFileEntries())
            {
                AdvanceCurrentFileIndex();
                fileChanged = true;
                _entriesDumpedToFile = 0;
            }

            _writer = OpenFileStream(fileChanged);
        }

        /// <summary>
        /// Given a FileInfo file, where the filename has an index, get the next int index
        /// </summary>
        private static int GetFileIndex(FileInfo file)
        {
            if(file is null) { return 0; }

            string fileName = file.Name;
            string index = fileName.Split('_')[1].Split('.')[0];
            return int.Parse(index);
        }
        
        /// <summary>
        /// Move file index to the next index
        /// </summary>
        private static void AdvanceCurrentFileIndex()
        {
            _currentFileIndex = (_currentFileIndex+1) % _maxFiles;
        }

        /// <summary>
        /// Checks if the number of entries dumped to the log file is greater than or equal to the maximum number of entries per file.
        /// </summary>
        private static bool RoundRobinFileEntries()
        {
            if (_entriesDumpedToFile >= _maxEntriesPerFile)
            {
                //entriesDumpedToFile = 0;
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Read the file and count the number of lines, returns zero (0) if file does not exist
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        private static int GetEntriesDumpedToFiles(FileInfo file)
        {
            if(File.Exists(file.FullName) == false) { return 0; }
            return File.ReadLines(file.FullName).Count();
            
        }
        
        /// <summary>
        /// This method is used to dump log files periodically.
        /// It takes the current index and the last index dumped to file
        /// It calculates the fromIndex by taking the modulus of lastIndexDumpedtofile and the length of ReceivedDataQueue items.
        /// If the currentIndex is greater than or equal to fromIndex, it means we are still in the same round robin cycle.
        /// In this case, it dumps the new entries to the log file from fromIndex to currentIndex.
        /// If the currentIndex is less than fromIndex, it means we are in the next round robin cycle.
        /// In this case, it dumps the new entries to the log file from fromIndex to the end and from the start to currentIndex.
        /// </summary>
        /// <param name="currentIndex">The current index in the round robin cycle.</param>
        /// <param name="lastIndexDumpedtofile">The last index that was dumped to the file.</param>
        private static void DumpLogToFiles()
        {
            var queueSnapshot = _receivedDataQueue.GetSnapshot();

            int currentIndex = queueSnapshot.CurrentIndex;
            int fromIndex = LastDumpToFileIndex % queueSnapshot.LogMessages.Length;

            if (fromIndex == currentIndex)
            {
                //Index did not change, no new messages
                return;
            }

            _logger.LogInformation($"Will dump to log file from Index {fromIndex} to {currentIndex}");

            //If has valid messages
            if (!queueSnapshot.LogMessages.Any(clm => clm.ApplicationKey != Guid.Empty))
            {
                LastDumpToFileIndex = currentIndex;
                return;
            }

            if (currentIndex >= fromIndex)
            {
                // still in the same round robin cycle

                //dump the new  entries to the log file
                DumpToFile(queueSnapshot.LogMessages.ToArray()[fromIndex..currentIndex]);
            }
            else if (currentIndex < fromIndex)
            {
                // we are in the round robin cycle

                //dump the new  entries to the log file
                DumpToFile(queueSnapshot.LogMessages.ToArray()[fromIndex..]);
                DumpToFile(queueSnapshot.LogMessages.ToArray()[0..currentIndex]);
            }

            _writer.Flush();
            //Update the last dump to file index with the current index
            LastDumpToFileIndex = currentIndex;
        }

        /// <summary>
        /// Dumps an array of log messages to the log file
        /// </summary>
        private static void DumpToFile(LogMessage[] buffer)
        {
            try
            {
                // Write the buffer to the log file
                foreach (var entry in buffer)
                {
                    //Do not log empty entries
                    if(entry.ApplicationKey == Guid.Empty) { continue; }
                    LogMessageFileHandler.WriteLogEntry(_writer, entry);
                    //_writer?.WriteLine(System.Text.Json.JsonSerializer.Serialize(entry));
                    _entriesDumpedToFile++;

                    if (RoundRobinFileEntries())
                    {
                        OpenNextStreamWriter();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error writing to log file: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Closes the current writer to dispose elegantly of the resources
        /// </summary>
        private static void Close()
        {
            _writer?.Dispose();
        }

        #endregion

        #region Main Flushing Thread

        public static void Pulse()
        {
            lock(_flushingThreadLock)
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
                lock(_flushingThreadLock)
                {
                    _logger.LogInformation("LogFileManager FlushingThread waked up");
                    DumpLogToFiles();
                    Monitor.Wait(_flushingThreadLock, _configuration.FlushTimeSeconds * 1000);
                }
            }
        }

        #endregion
    }

    public class LogFileManagerConfiguration
    {
        /// <summary>
        /// The path of the log files. Default is the "Timelog" folder inside the current user temporary folder.
        /// </summary>
        public string LogFilesPath { get; set; } = Path.Combine(Path.GetTempPath(), "Timelog");

        /// <summary>
        /// The maximum number of log files to be created. Log files are rotating. Default is 10.
        /// </summary>
        public int MaxLogFiles { get; set; } = 10;

        /// <summary>
        /// The maximum number of entries per log file. Default is 100000.
        /// </summary>
        public int MaxLogFileEntries { get; set; } = 100000;

        /// <summary>
        /// The number of entries that force a flush to log file. Default is 20000.
        /// </summary>
        public int FlushItemsSize { get; set; } = 20000;

        /// <summary>
        /// The number of seconds that force a flush to log file. Default is 30 seconds.
        /// </summary>
        public int FlushTimeSeconds { get; set; } = 30;
    }
}
