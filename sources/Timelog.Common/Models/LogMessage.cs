using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Timelog.Common.Models
{
    public enum Commands
    {
        None = 0,
        Start = 1,
        Stop = 2,
        Wait = 3,
    }

    [Serializable]
    
    public struct LogMessage
    {
        
        public Guid ApplicationKey;

        public byte[] Domain;
        
        public int LogLevelClient;
        
        public string TagClient;

        public Guid TransactionID;

        public Commands Command;

        public DateTime? OriginTimestamp;

        public DateTime? TimeServerTimeStamp;

        public object Reserved;

        public Message Message;
    }

    [Serializable]
    public struct Message
    {
        public string Header;

        public byte[] Data;

    }
}
