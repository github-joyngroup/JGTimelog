using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Joyn.Timelog.Common.Models
{
    public static class Constants
    {
        public const string TimelogTCPOperationKey = "Operation";
    }

    /// <summary>
    /// Identifies the operation being executed when a message is sent to the ReportingServer via TCP channel
    /// </summary>
    public enum TimelogTCPOperation
    {
        //0 - 100 : Control
        None = 0,
        Connect = 5,
        Disconnect = 7,

        //100 - 200 : Client Requests
        Ping = 100,
        SetFilter = 101,
        GetFilter = 102,
        
        //200 - 300 : Server Requests
        CurrentFilter = 202,
        LogMessages = 203
    }

    public delegate void OnTimelogTCPOperationHandler (TimelogTCPOperation operation, Guid clientGuid, List<FilterCriteria> filters, List<LogMessage> logMessages);
    public delegate void OnLogMessagesReceivedHandler(List<LogMessage> logMessages);
}
