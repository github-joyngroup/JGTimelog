using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Timelog.Client
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

    }
}
