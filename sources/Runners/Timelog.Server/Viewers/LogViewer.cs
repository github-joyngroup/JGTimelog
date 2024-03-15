using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Timelog.Common.Models;

namespace Timelog.Server.Viewers
{
    /// <summary>
    /// Represents a LogViewer Entry in the authorized or current viewers list
    /// </summary>
    public class LogViewer
    {
        /// <summary>
        /// The Application Key that identifies the viewer
        /// </summary>
        public Guid ApplicationKey { get; set; }

        /// <summary>
        /// The bit mask contribution of this viewer. It is calculated based on it's position on the configuration array
        /// The value will be 1 shifted to the left by the index of the viewer
        /// </summary>
        public int Bitmask { get; set; }

        /// <summary>
        /// Current filters in place for this viewer
        /// </summary>
        public List<FilterCriteria> Filters { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        public LogViewer(Guid applicationKey, int index)
        {
            ApplicationKey = applicationKey;
            Bitmask = 1 << index;
        }
    }
}
