using Microsoft.Extensions.Logging;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.Intrinsics.Arm;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Timelog.Common.Models
{
    /// <summary>
    /// Filter Criteria that define a way to filter the log messages
    /// </summary>
    [ProtoContract]
    public class FilterCriteria
    {
        //Header Fields
        /// <summary>
        /// Guid of the TimeLogServer - used only for requests between the TimeLog Reporting and the TimeLog Server
        /// </summary>
        [ProtoMember(1)]
        public Guid? TimeLogServerGuid { get; set; }

        /// <summary>
        /// Guid of the TimeLog Reporting - used only for requests between the TimeLog Reporting and the TimeLog Server
        /// </summary>
        [ProtoMember(2)]
        public Guid? ReportingServerGuid { get; set; }

        /// <summary>
        /// Guid of the End Viewer that placed the filter request
        /// </summary>
        [ProtoMember(3)]
        public Guid? ViewerGuid { get; set; }

        //Control fields do allow changing the state of the filter

        /// <summary>
        /// A value from the StateEnum converted to Integer
        /// </summary>
        [ProtoMember(4)]
        public int StateCode { get; set; }

        /// <summary>
        /// State of the filter
        /// </summary>
        public FilterCriteriaState State { get { return (FilterCriteriaState)StateCode; } }

        //Log Message Filtering Fields

        /// <summary>The application key of the application where the log was generated</summary>
        [ProtoMember(5)]
        public Guid? ApplicationKey { get; set; }

        /// <summary>
        /// 4 bytes that represent the base domain - should be parsable to an IP address
        /// </summary>
        [ProtoMember(6)]
        public uint? BaseDomain { get; set; }

        /// <summary>
        /// 4 bytes that represent the domain mask - together with BaseDomain will allow network like filtering over log message domains
        /// </summary>
        [ProtoMember(7)]
        public uint? DomainMask { get; set; }

        /// <summary>
        /// The maximum log level that the client is interested in. Filter will return messages with log level less than or equal to this value
        /// i.e. if this is set to 5, the filter will return messages with log level <= 5
        /// </summary>
        [ProtoMember(8)]
        public int? MaxLogLevelClient { get; set; }

        /// <summary>
        /// The guid of the transaction we're interested in
        /// </summary>
        [ProtoMember(9)]
        public Guid? TransactionID { get; set; }

        [ProtoMember(13)]
        public HashSet<Guid> TransactionIDs { get; set; }

        /// <summary>
        /// A byte array that filters by command of the log message
        /// </summary>
        [ProtoMember(10)]
        public Commands? CommandMask { get; set; }

        /// <summary>
        /// The beginning of the server timestamp, we want messages that are newer than this
        /// </summary>
        [ProtoMember(11)]
        public DateTime? BeginServerTimestamp { get; set; }

        /// <summary>
        /// The end of the server timestamp, we want messages that are older than this
        /// </summary>
        [ProtoMember(12)]
        public DateTime? EndServerTimestamp { get; set; }

        //EPocas - discontinued to remove dependency on DocDigitizer.Common
        //If Hash became required, we can reaccess this field
        ///// <summary>
        ///// Lazy loads the hash of the filter, all fields are accounted except for the Guid ones
        ///// </summary>
        //private string hash;
        //public string Hash
        //{
        //    get
        //    {
        //        if (string.IsNullOrWhiteSpace(hash))
        //        {
        //            var baseHash = $"{StateCode}|{DomainMask}|{MaxLogLevelClient}|{TransactionID}|{CommandMask}|{BeginServerTimestamp}|{EndServerTimestamp}";
        //            hash = DocDigitizer.Common.Security.Crypto.Hashing.MD5Hashing.SingletonMD5Hasher.Instance.Hash(baseHash);
        //        }
        //        return hash;
        //    }
        //}

        /// <summary>
        /// Check if this filter is interested in the received message
        /// </summary>
        /// <param name="logMessage"></param>
        /// <returns></returns>
        public bool Matches(LogMessage logMessage)
        {
            bool retBool = true;
            //Check Log Level
            retBool = retBool && (MaxLogLevelClient is null || logMessage.ClientLogLevel <= MaxLogLevelClient.Value);
            //Check application key
            retBool = retBool && (ApplicationKey is null || logMessage.ApplicationKey == ApplicationKey.Value);
            //Check Domain
            if(BaseDomain is not null && DomainMask is not null)
            {
                //Similar to IP networks, we will calculate the mask network first by applying the mask to the domain
                uint domainMaskNetwork = BaseDomain.Value & DomainMask.Value;
                //We will then calculate the log message network by applying the same mask to the log message domain
                uint logNetwork = logMessage.Domain & DomainMask.Value;
                //Filter is interested in the message only if the networks match
                retBool = retBool && (domainMaskNetwork == logNetwork);
            }
            //Check Transaction Id
            retBool = retBool && (TransactionID is null || logMessage.TransactionID == TransactionID.Value);
            //Check Transaction Ids
            retBool = retBool && (TransactionIDs is null || TransactionIDs.Contains(logMessage.TransactionID));
            //Check Command
            retBool = retBool && (CommandMask is null || logMessage.Command == CommandMask.Value);
            //Check Begin Server Timestamp
            retBool = retBool && (BeginServerTimestamp is null || logMessage.TimeServerTimeStamp >= BeginServerTimestamp.Value);
            //Check End Server Timestamp
            retBool = retBool && (EndServerTimestamp is null || logMessage.TimeServerTimeStamp <= EndServerTimestamp.Value);

            return retBool;
        }
    }
 
    /// <summary>
    /// States that a filter can be in, should be converted to integer and values shall be powers of 2 to be used as a bit mask
    /// </summary>
    public enum FilterCriteriaState
    {
        /// <summary>
        /// The filter is Paused, no messages will be returned
        /// </summary>
        Paused = 0,
        /// <summary>
        /// The filter is On, messages will be returned live
        /// </summary>
        On = 1,
        /// <summary>The filter is in search mode, log files will be searched for matches</summary>
        Search = 2,

        /// <summary>
        /// Not defined values for future use
        /// </summary>
        NotDefined4 = 4,
        NotDefined8 = 8,
        NotDefined16 = 16,
        NotDefined32 = 32,
        NotDefined64 = 64,
        NotDefined128 = 128,
        NotDefined256 = 256,
        NotDefined512 = 512,
    }
}
