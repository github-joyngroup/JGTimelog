using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Timelog.Common.Models;

namespace Timelog.Common
{
    /// <summary>
    /// Helper class to handle LogMessage files
    /// </summary>
    public static class LogMessageFileHandler
    {
        /// <summary>
        /// Delimiter to separate entries in the log file, cannot be a const because initializer is not a constant
        /// </summary>
        private static readonly byte[] entryDelimiter = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF };

        /// <summary>
        /// Writes a single LogMessage to a FileStream, note that the FileStream should be opened with FileAccess.Write
        /// </summary>
        public static void WriteLogEntry(FileStream fileStream, LogMessage logEntry)
        {
            var entryBytes = ProtoBufSerializer.Serialize(logEntry);
            byte[] lengthPrefix = BitConverter.GetBytes(entryBytes.Length);
            //Write the length prefix
            fileStream.Write(lengthPrefix, 0, lengthPrefix.Length);
            //Write the entry
            fileStream.Write(entryBytes, 0, entryBytes.Length);
            //Write the delimiter
            fileStream.Write(entryDelimiter, 0, entryDelimiter.Length);
        }

        /// <summary>
        /// Reads all LogMessages from a given file.
        /// If the file content is not consistent with the expected format, an exception is thrown
        /// </summary>
        public static List<LogMessage> ReadLogMessages(string filePath)
        {
            List<LogMessage> retList = new List<LogMessage>();

            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                while(fileStream.Position < fileStream.Length)
                {
                    //Read the Length Prefix
                    if(fileStream.Position + 4 > fileStream.Length)
                    {
                        throw new FileLoadException($"Log File invalid - could not read length on position {fileStream.Position}");
                    }
                    byte[] lengthPrefix = new byte[4];
                    fileStream.Read(lengthPrefix, 0, lengthPrefix.Length);
                    int entryLength = BitConverter.ToInt32(lengthPrefix, 0);

                    //Read as many bytes as the length prefix indicates
                    if (fileStream.Position + entryLength > fileStream.Length)
                    {
                        throw new FileLoadException($"Log File invalid - could not read entry on position {fileStream.Position}");
                    }
                    byte[] entryBytes = new byte[entryLength];
                    fileStream.Read(entryBytes, 0, entryBytes.Length);

                    //Read delimiter bytes
                    if (fileStream.Position + entryDelimiter.Length > fileStream.Length)
                    {
                        throw new FileLoadException($"Log File invalid - could not read delimiter on position {fileStream.Position}");
                    }
                    byte[] entryDelimiterRead = new byte[entryDelimiter.Length];
                    fileStream.Read(entryDelimiterRead, 0, entryDelimiter.Length);

                    if(entryDelimiterRead.SequenceEqual(entryDelimiter) == false)
                    {
                        throw new FileLoadException($"Entry Delimiter not matches delimiter on position {fileStream.Position}");
                    }
                    
                    //Deserialize the entry
                    LogMessage logMessage = ProtoBufSerializer.Deserialize<LogMessage>(entryBytes);
                    retList.Add(logMessage);
                }

            }

            return retList;
        }

        /// <summary>
        /// Will filter all log files in the given directory and return only the messages that match the filter criteria
        /// </summary>
        public static List<LogMessage> SearchLogFiles(FilterCriteria filterCriteria, string directoryPath)
        {
            List<LogMessage> retList = new List<LogMessage>();

            var files = Directory.GetFiles(directoryPath);
            foreach (var file in files)
            {
                var logEntries = ReadLogMessages(file);
                retList.AddRange(logEntries.Where(le => filterCriteria.Matches(le)));
            }

            return retList;
        }
    }
}
