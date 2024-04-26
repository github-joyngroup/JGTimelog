using Microsoft.Extensions.Logging;
using ProtoBuf;
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
        Normal = 1,
        Start = 2,
        Stop = 3,
    }

    [ProtoContract]
    public struct LogMessage
    {
        /// <summary>The application key of the applicatin where the log was generated</summary>
        [ProtoMember(1)]
        public Guid ApplicationKey { get; set; }

        /// <summary>
        /// 4 bytes that represent the domain - will be handled as an IP address for filtering purposes
        /// </summary>
        [ProtoMember(2)] 
        public uint Domain { get; set; }

        /// <summary>
        /// Log Level of the message
        /// </summary>
        [ProtoMember(3)]
        public int ClientLogLevel { get; set; }

        /// <summary>
        /// 8 bytes to be used by the client to tag the message with a client specific value, typically maps to an enum
        /// </summary>
        [ProtoMember(4)]
        public long ClientTag { get; set; }

        /// <summary>
        /// Id of the transaction, generally used to group several logs together to "tell a story"
        /// </summary>
        [ProtoMember(5)]
        public Guid TransactionId { get; set; }

        /// <summary>
        /// Command associated with the log message
        /// </summary>
        [ProtoMember(6)]
        public Commands Command { get; set; }

        /// <summary>
        /// Timestamp of the log message on the originator server
        /// </summary>
        [ProtoMember(7)]
        public DateTime? OriginTimestamp { get; set; }

        /// <summary>
        /// Calculated between Start/Stop commands
        /// </summary>
        [ProtoMember(8)]
        public TimeSpan? ExecutionTime { get; set; }
        
        /// <summary>
        /// Timestamp of the log message on the server, filled by the Timelog Server upon reception of the message
        /// </summary>
        [ProtoMember(9)]
        public DateTime? TimeServerTimeStamp { get; set; }

        /// <summary>
        /// Reserved for future use
        /// </summary>
        [ProtoMember(10)]
        public byte[] Reserved { get; set; }

        /// <summary>
        /// Up to 128 bytes that represent the header of the message, client defined 
        /// </summary>
        [ProtoMember(11)]
        public byte[] MessageHeader { get; set; }

        /// <summary>
        /// Up to 1024 bytes that represent the data of the message, client defined 
        /// </summary>
        [ProtoMember(12)]
        public byte[] MessageData { get; set; }

        /// <summary>
        /// Will identify the viewers interested in this message, i.e. those that have any filter that matches this message
        /// </summary>
        [ProtoMember(13)]
        public long FilterBitmask { get; set; }

        /// <summary>
        /// Optional, Id of the execution, generally used to distinguish between executions of the same method/activity/step within a transaction
        /// </summary>
        [ProtoMember(14)]
        public Guid? ExecutionId { get; set; }

    }
}
