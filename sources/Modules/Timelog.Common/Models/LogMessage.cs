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
        /// 4 bytes to be used by the client to tag the message with a client specific value
        /// </summary>
        public byte[] TagClient;

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
        /// 128 bytes that represent the header of the message, client defined 
        /// </summary>
        public byte[] MessageHeader;

        /// <summary>
        /// 1024 bytes that represent the data of the message, client defined 
        /// </summary>
        public byte[] MessageData;

        /// <summary>
        /// Will identify the viewers interested in this message, i.e. those that have any filter that matches this message
        /// </summary>
        public long FilterBitmask;
    }
}
