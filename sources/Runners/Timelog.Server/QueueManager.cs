//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using Timelog.Common.Models;


//namespace Timelog.Server
//{
//    internal class QueueManager
//    {
//        private readonly RoundRobinArray<LogMessage>? receivedDataQueue;

//        public event Action<LogMessage>? NewLogMessage;

//        public QueueManager(int maxQueueSize)
//        {
//            receivedDataQueue = new RoundRobinArray<LogMessage>(maxQueueSize);
//        }

//        public void LogHandler(LogMessage receivedData)
//        {
//            //if (receivedData is null) { return; }

//            receivedDataQueue?.Add(receivedData);

//            // Raise the event asynchronously in a background task
//            Task.Run(() => NewLogMessage?.Invoke(receivedData));
//        }
//    }

    
//}
