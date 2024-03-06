using Microsoft.Extensions.Configuration;
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

        /// <summary>
        /// The Timelog level
        /// </summary>
        public int LogLevel { get; set; }

        /// <summary>
        /// Client Timestamp
        /// </summary>
        public bool UseClientTimestamp { get; set; }


        public static Configuration ReadConfiguration(string filePath)
        {
            var configuration = new Configuration();
            var configurationBuilder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile(filePath);

            IConfigurationRoot configRoot = configurationBuilder.Build();
            configRoot.Bind(configuration);

            // Add additional validation if needed

            return configuration;
        }
    }
}

