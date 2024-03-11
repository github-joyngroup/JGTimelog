using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Timelog.Reporting
{
    public class Configuration
    {
        /// <summary>
        /// The FQDN of the Timelog server or their IP address
        /// </summary>
        public string TimelogServerHost { get; set; }

        /// <summary>
        /// The Timelog server network port number
        /// </summary>
        public int TimelogServerPort { get; set; }

        /// <summary>
        /// The Named Pipe of the Timelog server or their IP address
        /// </summary>
        public string TimelogServerNamedPipe { get; set; }

        /// <summary>
        /// The Named Pipe App Key to be used when connecting to the Timelog server
        /// </summary>
        public string TimelogServerNamedPipeAppKey { get; set; }

        /// <summary>
        /// The path of a json file with the Access Control List (ACL), of the clients that will be authorized to communicate with the server.
        /// </summary>
        public string AuthorizationsFilePath { get; set; }

        public List<Guid> AuthorizedAppKeys { get; set; }
    }
}
