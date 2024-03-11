using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Timelog.ClientReport
{
    public class Configuration
    {
        /// <summary>
        /// The unique identifier of the client application.
        /// </summary>
        public Guid ApplicationKey { get; set; }

        /// <summary>
        /// The FQDN of the Timelog server or their IP address
        /// </summary>
        public string TimelogServerHost { get; set; }

        /// <summary>
        /// The Timelog server network port number
        /// </summary>
        public int TimelogServerPort { get; set; }

        /// <summary>
        /// The Timelog level
        /// </summary>
        public LogLevel LogLevel { get; set; }

        /// <summary>
        /// Client Timestamp
        /// </summary>
        public bool UseClientTimestamp { get; set; }
    }
}

