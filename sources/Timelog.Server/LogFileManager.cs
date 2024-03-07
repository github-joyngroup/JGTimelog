using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Timelog.Common;
using Timelog.Common.Models;

namespace Timelog.Server
{
    internal class LogFileManager
    {
        private LogMessage[] bufferA;
        private LogMessage[] bufferB;
        private bool isBufferA;
        private int currentFileIndex;
        private int currentBufferIndex;
        private int entriesDumpedToFile;
        private readonly int _maxBufferSize;

        private readonly string _logFilePath;
        private readonly QueueManager _queueManager;

        private readonly int _maxFiles;
        private readonly int _maxEntriesPerFile;
        private readonly object _lock = new();


        public LogFileManager(string logFilePath, int maxFiles = 10, int maxEntriesPerFile = 1024, int maxBufferSize = 256)
        {
            _logFilePath = Path.Combine(logFilePath, "Timelog_{0}.txt");
            
            _maxBufferSize = maxBufferSize;
            bufferA = new LogMessage[_maxBufferSize];
            bufferB = new LogMessage[_maxBufferSize];
            isBufferA = true;
            currentFileIndex = 0;
            currentBufferIndex = 0;
            entriesDumpedToFile = 0;
            _maxFiles = maxFiles;
            _maxEntriesPerFile = maxEntriesPerFile;

        }

        public LogFileManager(string logFilePath, QueueManager queueManager, int maxFiles = 10, int maxEntriesPerFile = 1024, int maxBufferSize = 256)
        {
            _logFilePath = Path.Combine(logFilePath, "Timelog_{0}.txt");
            _queueManager = queueManager;
            _maxBufferSize = maxBufferSize;
            bufferA = new LogMessage[_maxBufferSize];
            bufferB = new LogMessage[_maxBufferSize];
            isBufferA = true;
            currentFileIndex = 0;
            currentBufferIndex = 0;
            entriesDumpedToFile = 0;
            _maxFiles = maxFiles;
            _maxEntriesPerFile = maxEntriesPerFile;

            _queueManager.NewLogMessage += OnNewLogMessage;

            //Start a background task to dump the log files periodically
            //Task.Run(() => DumpFilesPeriodically());
        }

        private void OnNewLogMessage(LogMessage logMessage)
        {
            //if (logMessage is null) { return; }

            lock (_lock)
            {
                if (isBufferA)
                {
                    // Add the log message to buffer A
                    bufferA[currentBufferIndex] = logMessage;
                }
                else
                {
                    // Add the log message to buffer B
                    bufferB[currentBufferIndex] = logMessage;
                }

                currentBufferIndex++;

                if (currentBufferIndex >= _maxBufferSize)
                {
                    DumpCurrentBuffer();
                    currentBufferIndex = 0;
                    isBufferA = !isBufferA;
                }

                entriesDumpedToFile++;

                if (entriesDumpedToFile >= _maxEntriesPerFile)
                {
                    DumpCurrentBuffer();
                    currentBufferIndex = 0;
                }

            }
        }

        private void DumpCurrentBuffer() //Note: this is running inside a lock
        {
            var currentBuffer = isBufferA ? bufferA : bufferB;
            int currentIndex = currentBufferIndex;
            int currentFile = currentFileIndex;
            bool maxEntriesReached = entriesDumpedToFile >= _maxEntriesPerFile;

            Task.Run(() =>
            {
                DumpToFile(currentFile, currentBuffer, resetIfExists: maxEntriesReached);
                Array.Clear(currentBuffer);
            });

            if (maxEntriesReached)
            {
                currentFileIndex = (currentFileIndex++) % _maxFiles;
                entriesDumpedToFile = 0; // Reset entries dumped to the current file
            }

        }

        private void DumpFilesPeriodically()
        {
            while (true)
            {
                // Dump files every few seconds, if there are any entries in the buffer
                Thread.Sleep(TimeSpan.FromSeconds(30));

                if (currentBufferIndex > 0)
                {
                    lock (_lock)
                    {
                        DumpCurrentBuffer();
                    }
                }
            }
        }

        public void DumpToFile(int index, LogMessage[] buffer, bool resetIfExists = true)
        {
            //if (buffer == null || !buffer.Any(entry => entry != null)) { return; }
            string filePath = string.Format(_logFilePath, index.ToString().PadLeft(5, '0'));

            // If the file exists but it's older than today than reset it anyway
            if (!resetIfExists && File.Exists(filePath) && DateTime.Now.DayOfYear > File.GetLastWriteTime(filePath).DayOfYear)
            {
                resetIfExists = true;
            }

            try
            {
                var list = buffer.Where(entry => entry.ApplicationKey != Guid.Empty);
                DateTime oldestTimestamp = list != null && list.Any() ? list.Min(entry => entry.TimeServerTimeStamp.Value) : DateTime.MinValue;
                DateTime newestTimestamp = list != null && list.Any() ? list.Max(entry => entry.TimeServerTimeStamp.Value) : DateTime.MinValue;

                JsonSerializerOptions options = new JsonSerializerOptions
                {
                    IncludeFields = true
                };

                // Write the buffer to the log file, appending if the file already exists and not resetting
                using (StreamWriter writer = new StreamWriter(filePath, append: !resetIfExists))
                {
                    writer.WriteLine($"BeginAt: {oldestTimestamp.Ticks}");
                    writer.WriteLine($"EndAt: {newestTimestamp.Ticks}");
                    foreach (var entry in buffer)
                    {
                        if (entry.ApplicationKey != Guid.Empty)
                        {
                            //writer.WriteLine(System.Text.Json.JsonSerializer.Serialize(entry, options));
                            writer.WriteLine($"{entry.Domain},{entry.OriginTimestamp},{entry.TimeServerTimeStamp}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error writing to log file '{filePath}': {ex.Message}");
            }
        }

        //public void DumpToFileBinary(int index, LogMessage[] buffer, bool resetIfExists = true)
        //{
        //    //if (buffer == null || !buffer.Any(entry => entry != null)) { return; }
        //    string filePath = string.Format(_logFilePath, index.ToString().PadLeft(5, '0'));

        //    // If the file exists but it's older than today than reset it anyway
        //    if (!resetIfExists && File.Exists(filePath) && DateTime.Now.DayOfYear > File.GetLastWriteTime(filePath).DayOfYear)
        //    {
        //        resetIfExists = true;
        //    }

        //    try
        //    {
        //        DateTime oldestTimestamp = buffer//.Where(entry => entry != null)
        //                                         .Min(entry => entry.TimeServerTimeStamp.Value);
        //        DateTime newestTimestamp = buffer//.Where(entry => entry != null)
        //                                         .Max(entry => entry.TimeServerTimeStamp.Value);

        //        using (StreamWriter writer = new StreamWriter(filePath, append: !resetIfExists))
        //        {
        //            writer.WriteLine($"BeginAt: {oldestTimestamp.Ticks}");
        //            writer.WriteLine($"EndAt: {newestTimestamp.Ticks}");
        //            //foreach (var entry in buffer)
        //            //{
        //            //    writer.WriteLine(Convert.ToBase64String(ByteSerializer<LogMessage>.Serialize(entry)));
                        
        //            //}
        //        }

        //        //using (FileStream writer = new FileStream(filePath, FileMode.Append))
        //        //{
        //        //    BinaryFormatter formatter = new BinaryFormatter();
        //        //    foreach (var entry in buffer)
        //        //    {
        //        //        formatter.Serialize(writer, entry);
        //        //    }
        //        //}

        //        using (BinaryWriter writer = new BinaryWriter(File.Open(filePath, FileMode.Append)))
        //        {
        //            BinaryFormatter formatter = new BinaryFormatter();
        //            foreach (var entry in buffer)
        //            {
        //                formatter.Serialize(writer.BaseStream, entry);
        //            }
        //        }
                
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"Error writing to log file '{filePath}': {ex.Message}");
        //    }
        //}


    }
}
