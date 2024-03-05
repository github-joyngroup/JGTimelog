using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Timelog.Server
{
    public class Configuration
    {
        /// <summary>
        /// The path of a json file with the Access Control List (ACL), of the clients that will be authorized to communicate with the server.
        /// </summary>
        public string AuthorizationsFilePath { get; set; }

        public List<Guid> AuthorizedAppKeys { get; set; }

        /// <summary>
        /// The Timelog server network port number, that will be open to receive the logs sent by the client. Default is 7777.
        /// </summary>
        public int TimelogServerPort { get; set; } = 7777;

        /// <summary>
        /// The number of entries to be accepted on the global internal cache. Default is 1024.
        /// </summary>
        public int InternalCacheMaxEntries { get; set; } = 10;

        /// <summary>
        /// The maximum number of viewers at the same time. Default is 64.
        /// </summary>
        public int MaxNumberOfViewers { get; set; } = 64;

        /// <summary>
        /// The maximum number of log files to be created. Log files are rotating. Default is 20.
        /// </summary>
        public int MaxLogFiles { get; set; } = 20;

        /// <summary>
        /// The maximum number of entries per log file. Default is 100000.
        /// </summary>
        public int MaxLogFileEntries { get; set; } = 100000;

        /// <summary>
        /// The network port number that the viewer register will be listening. Default is 8888.
        /// </summary>
        public int TimelogViewerRegisterPort { get; set; } = 8888;
    }
}
