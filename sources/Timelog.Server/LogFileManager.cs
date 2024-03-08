using System;
using System.Collections.Generic;
using System.IO;
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
        private int currentFileIndex;

        private int entriesDumpedToFile;
        //the last read index from the ReceivedDataQueue
        private (int roundRobinCounter, int lastReadIndex) lastQueueRead;

        private readonly string _logFilesPath;
        private readonly int _maxFiles;
        private readonly int _maxEntriesPerFile;
        private readonly object _lock = new();
        private RoundRobinArray<LogMessage>? ReceivedDataQueue;
        private StreamWriter? _writer;

        public LogFileManager(RoundRobinArray<LogMessage>? receivedDataQueue, string logFilesPath, int maxFiles, int maxEntriesPerFile)
        {
            if(!Directory.Exists(logFilesPath)) { throw new DirectoryNotFoundException($"The directory '{logFilesPath}' does not exist.");}

            _logFilesPath = logFilesPath;
            _maxFiles = maxFiles;
            _maxEntriesPerFile = maxEntriesPerFile;

            ReceivedDataQueue = receivedDataQueue;
            var _lastFile = GetLastFilePath(_logFilesPath);
            currentFileIndex = GetFileIndex(_lastFile);
            entriesDumpedToFile = GetEntriesDumpedToFiles(_lastFile);
            lastQueueRead.lastReadIndex = 0;
            lastQueueRead.roundRobinCounter = 0;
            OpenNextStreamWriter();

            

        }

        private string GetFilePath()
        {
            string filePath = string.Format(Path.Combine(_logFilesPath, "Timelog_{0}.txt"), currentFileIndex.ToString().PadLeft(5, '0'));
            return filePath;
        }
        private StreamWriter OpenStreamWriter(bool fileChanged)
        {
            string filePath = GetFilePath();
            return new StreamWriter(filePath, append: !fileChanged);
        }
        private void OpenNextStreamWriter()
        {
            if (_writer != null)
            {
                _writer.Dispose();
            }
            bool fileChanged = false;
            //currentFileIndex = GetFileIndex(GetLastFilePath(_logFilesPath));
            if (RoundRobinFileEntries())
            {
                GetNextFileIndex();
                fileChanged = true;
                entriesDumpedToFile = 0;
            }

            _writer = OpenStreamWriter(fileChanged);
        }

        private FileInfo GetLastFilePath(string fileDirectoryPath)
        {
            //Find the last written file in the directory
            // and get the file with most recent date
            var directory = new DirectoryInfo(fileDirectoryPath);
            var files = directory.GetFiles();
            
            if(files.Length == 0) { return new FileInfo(GetFilePath()); }

            var myFile = (from f in files
                          orderby f.LastWriteTime descending
                          select f).First();
            
            return myFile;
        }

        //Given a FileInfo file, where the filename has an index, get the next int index
        private int GetFileIndex(FileInfo file)
        {
            if(file is null) { return 0; }

            string fileName = file.Name;
            string index = fileName.Split('_')[1].Split('.')[0];
            return int.Parse(index);
        }
        
        private void GetNextFileIndex()
        {
            currentFileIndex = (currentFileIndex+1) % _maxFiles;
        }

        private int GetEntriesDumpedToFiles(FileInfo file)
        {
            //Read the file and count the number of lines
            return File.ReadLines(file.FullName).Count();
            
        }
        public void DumpFilesPeriodically(int currentIndex, int lastIndexDumpedtofile)
        {
            int fromIndex = lastIndexDumpedtofile++ % ReceivedDataQueue.GetItems().Length;

            if (currentIndex >= fromIndex)
            {
                // still in the same round robin cycle

                //dump the new  entries to the log file
                DumpToFile(ReceivedDataQueue.GetItems().Where(e => e.ApplicationKey != Guid.Empty).ToArray()[fromIndex..currentIndex]);
            }
            else if (currentIndex < fromIndex)
            {
                // we are in the round robin cycle
                
                //dump the new  entries to the log file
                DumpToFile(ReceivedDataQueue.GetItems().Where(e => e.ApplicationKey != Guid.Empty).ToArray()[fromIndex..]);
                DumpToFile(ReceivedDataQueue.GetItems().Where(e => e.ApplicationKey != Guid.Empty).ToArray()[0..currentIndex]);
            }
                
        }

        private bool RoundRobinFileEntries()
        {
            if(entriesDumpedToFile >= _maxEntriesPerFile)
            {
                //entriesDumpedToFile = 0;
                return true;
            }
            else
            {
                return false;
            }
        }

        

        public void DumpToFile(LogMessage[] buffer)
        {
 

            try
            {
                
                JsonSerializerOptions options = new JsonSerializerOptions
                {
                    IncludeFields = true
                };

                // Write the buffer to the log file
                foreach (var entry in buffer)
                {
                    _writer?.WriteLine(System.Text.Json.JsonSerializer.Serialize(entry, options));
                    entriesDumpedToFile++;

                    if (RoundRobinFileEntries())
                    {
                        OpenNextStreamWriter();
                    }
                }
                
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error writing to log file: {ex.Message}");
            }
        }

        public void Close()
        {
            _writer?.Dispose();
        }


    }
}
