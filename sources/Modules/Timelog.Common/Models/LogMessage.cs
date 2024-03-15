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

        /// <summary>The application key of the applicatin where the log was generated</summary>
        public Guid ApplicationKey;

        /// <summary>
        /// 4 bytes that represent the domain - should be parsable to an IP address
        /// </summary>
        public byte[] Domain;
        
        /// <summary>
        /// Log Level of the message
        /// </summary>
        public int LogLevelClient;

        /// <summary>
        /// ??????? TO BE DEFINED
        /// </summary>
        public string TagClient;

        /// <summary>
        /// Id of the transaction, generally used to group several logs together to "tell a story"
        /// </summary>
        public Guid TransactionID;

        /// <summary>
        /// Command associated with the log message
        /// </summary>
        public Commands Command;

        /// <summary>
        /// Timestamp of the log message on the originator server
        /// </summary>
        public DateTime? OriginTimestamp;

        /// <summary>
        /// Timestamp of the log message on the server
        /// </summary>
        public DateTime? TimeServerTimeStamp;

        /// <summary>
        /// Reserved for future use
        /// </summary>
        public byte[] Reserved;

        /// <summary>
        /// Message associated with the log message
        /// </summary>
        public Message Message;

        /// <summary>
        /// Will identify the viewers interested in this message, i.e. those that have any filter that matches this message
        /// </summary>
        public long FilterBitmask;
    }

    [Serializable]
    public struct Message
    {
        public string Header;

        public byte[] Data;

    }
}
