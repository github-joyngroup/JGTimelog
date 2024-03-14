using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Timelog.Common.Models
{
    /// <summary>
    /// Filter Criteria that define a way to filter the log messages
    /// </summary>
    public class FilterCriteria
    {
        /// <summary>
        /// Guid of the TimeLogServer - used only for requests between the TimeLog Reporting and the TimeLog Server
        /// </summary>
        public Guid? TimeLogServerGuid { get; set; }

        /// <summary>
        /// Guid of the TimeLog Reporting - used only for requests between the TimeLog Reporting and the TimeLog Server
        /// </summary>
        public Guid? ReportingServerGuid { get; set; }

        /// <summary>
        /// Guid of the End Viewer that placed the filter request
        /// </summary>
        public Guid? ViewerGuid { get; set; }

        /// <summary>
        /// A value from the StateEnum converted to Integer
        /// </summary>
        public int StateCode { get; set; }

        /// <summary>
        /// State of the filter
        /// </summary>
        public FilterCriteriaState State { get { return (FilterCriteriaState) StateCode; } }

        /// <summary>
        /// 4 bytes that represent the domain - should be parsable to an IP address
        /// </summary>
        public byte[] DomainMask { get; set; }

        /// <summary>
        /// The maximum log level that the client is interested in. Filter will return messages with log level less than or equal to this value
        /// i.e. if this is set to 5, the filter will return messages with log level <= 5
        /// </summary>
        public int? MaxLogLevelClient { get; set; }

        /// <summary>
        /// The guid of the transaction we're interested in
        /// </summary>
        public Guid? TransactionID { get; set; }

        /// <summary>
        /// A byte array that filters by command of the log message
        /// </summary>
        public byte[] CommandMask { get; set; }

        /// <summary>
        /// The beginning of the server timestamp, we want messages that are newer than this
        /// </summary>
        public DateTime? BeginServerTimestamp { get; set; }

        /// <summary>
        /// The end of the server timestamp, we want messages that are older than this
        /// </summary>
        public DateTime? EndServerTimestamp { get; set; }

        /// <summary>
        /// Lazy loads the hash of the filter, all fields are accounted except for the Guid ones
        /// </summary>
        private string hash;
        public string Hash
        {
            get
            {
                if (string.IsNullOrWhiteSpace(hash))
                {
                    var baseHash = $"{StateCode}|{DomainMask}|{MaxLogLevelClient}|{TransactionID}|{CommandMask}|{BeginServerTimestamp}|{EndServerTimestamp}";
                    hash = DocDigitizer.Common.Security.Crypto.Hashing.MD5Hashing.SingletonMD5Hasher.Instance.Hash(baseHash);
                }
                return hash;
            }
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
        /// The filter is On, messages will be returned
        /// </summary>
        On = 1,

        /// <summary>
        /// Not defined values for future use
        /// </summary>
        NotDefined2 = 2,
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
