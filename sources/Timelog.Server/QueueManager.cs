using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Timelog.Common.Models;
using Timelog.Common;
using System.Collections.Concurrent;
using System.Collections.Immutable;

namespace Timelog.Server
{
    internal static class QueueManager
    {
        static RoundRobinArray<LogMessage>? receivedDataQueue;
        
        public static void Initialize(int maxQueueSize)
        {
            receivedDataQueue = new RoundRobinArray<LogMessage>(maxQueueSize);
        }

        public static void LogHandler(LogMessage? receivedData)
        {
            if (receivedDataQueue is null) { throw new ArgumentNullException("QueueManager was not initialized. Call the Initialize method before use it."); }
            if (receivedData is null) { return; }

            receivedDataQueue.Add(receivedData);

        }
    }
}
