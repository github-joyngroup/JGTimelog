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
    public class LogMessage
    {
        public Guid ApplicationKey { get; set; }

        public string Domain { get; set ; }

        public Guid TransactionID { get; set; }

        public Commands Command { get; set; }

        public DateTime? OriginTimestamp { get; set; }

        public DateTime? TimeServerTimeStamp { get; set;}

        public object Reserved { get; set; }

        public Message Message { get; set; }
    }

    [Serializable]
    public class Message
    {
        public string Header { get; set; }

        public byte[] Data { get; set; }

    }
}
