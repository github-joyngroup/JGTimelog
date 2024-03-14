using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Timelog.Common.Models;
using static Timelog.Server.Search.LogMessageSearch;

namespace Timelog.Server.Search
{
    public static class LogMessageSearch
    {
        public static FilterCriteria SearchCriteria = new ()
        {
            ViewerGuid = Guid.Parse("43e719fd-62bc-441f-80ba-cbb2a92ba44c")
        }; //{ get; set; }

        public static bool FilterMessagebyCriteria(int queueIndex)
        {
            var msg = Listener.ReceivedDataQueue[queueIndex];

            if (SearchCriteria.ViewerGuid.HasValue && msg.ApplicationKey != SearchCriteria.ViewerGuid)
            {
                return false;
            }
            if (SearchCriteria.TransactionID.HasValue && msg.TransactionID != SearchCriteria.TransactionID)
            {
                return false;
            }
            if (SearchCriteria.DomainMask is not null && !MatchesMask(msg.Domain, SearchCriteria.DomainMask))
            {
                return false;
            }
            if (SearchCriteria.MaxLogLevelClient.HasValue && msg.LogLevelClient > SearchCriteria.MaxLogLevelClient.Value)
            {
                return false;
            }   
            if (SearchCriteria.CommandMask is not null && !MatchesMask(BitConverter.GetBytes((int)msg.Command), SearchCriteria.CommandMask))
            {
                return false;
            }
            if (SearchCriteria.BeginServerTimestamp.HasValue && msg.TimeServerTimeStamp < SearchCriteria.BeginServerTimestamp)
            {
                return false;
            }
            if (SearchCriteria.EndServerTimestamp.HasValue && msg.TimeServerTimeStamp > SearchCriteria.EndServerTimestamp)
            {
                return false;
            }


            return true;
        }
        
        public static bool MatchesMask(byte[] domain, byte[] mask)
        {
            if (domain.Length != mask.Length)
            {
                return false;
            }

            for (int i = 0; i < domain.Length; i++)
            {
                // Apply the mask and compare
                if ((domain[i] & mask[i]) != domain[i])
                {
                    return false; // Does not match the mask
                }
            }

            return true; // All bytes match the mask
        }






        //static StreamWriter StreamWriter { get { if (_streamWriter is null) { _streamWriter = new StreamWriter("C:\\TEMP\\TimelogFiltered\\Timelog_filtered.txt"); } return _streamWriter; } }

        static JsonSerializerOptions options = new JsonSerializerOptions
        {
            IncludeFields = true
        };
        public static void DumpSearched(int currentIndex, StreamWriter streamWriter)
        {
            if(FilterMessagebyCriteria(currentIndex))
            {
                var line = System.Text.Json.JsonSerializer.Serialize(Listener.ReceivedDataQueue[currentIndex], options);
                streamWriter.WriteLine(line);
            }

        }


    }

}
