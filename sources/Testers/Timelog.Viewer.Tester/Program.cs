using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Timelog.Common.Models;
using Timelog.Viewer;

var builder = Host.CreateDefaultBuilder(args);

builder.ConfigureAppConfiguration((hostingContext, config) =>
{
    config.AddJsonFile("appsettings.json", optional: true);
    config.AddEnvironmentVariables();
});

builder.ConfigureServices((hostContext, services) =>
{
    services.AddLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddConsole();
        logging.AddFile(options => { hostContext.Configuration.GetSection("Logging:File").Bind(options); }); //Requires nuget NetEscapades.Extensions.Logging.RollingFile
    });

    services.AddHostedService<Timelog.Viewer.ReportingClient>();
});

var host = builder.Build();

var logger = host.Services.GetRequiredService<ILogger<MainTimelogViewer>>();
var configuration = host.Services.GetRequiredService<IConfiguration>();

logger.LogInformation("Starting... ");

var applicationKey = configuration.GetValue<Guid>("ApplicationKey");

var reportingClientConfiguration = configuration.GetSection("ReportingClient").Get<ReportingClientConfiguration>();
Timelog.Viewer.ReportingClient.Startup(applicationKey, reportingClientConfiguration, logger, HelperViewer.OnLogMessagesReceived);

logger.LogInformation("Running... ");

var hostTask = host.RunAsync();

FilterCriteria currentFilter = new FilterCriteria()
{
    ViewerGuid = applicationKey,
    ApplicationKey = null,
    StateCode = (int)FilterCriteriaState.Paused,
    DomainMask = null,
    MaxLogLevelClient = null,
    TransactionID = null,
    CommandMask = null,
    BeginServerTimestamp = null,
    EndServerTimestamp = null
};

HelperViewer.WriteInstructions();

var readConsole = "";
do
{
    readConsole = Console.ReadLine();
    if(readConsole == "show")
    {
        HelperViewer.ShowFilter(currentFilter);
    }
    else if (readConsole == "start")
    {
        currentFilter.StateCode = (int)FilterCriteriaState.On;
        Timelog.Viewer.ReportingClient.SetFilter(currentFilter);
    }
    else if (readConsole == "stop")
    {
        currentFilter.StateCode = (int)FilterCriteriaState.Paused;
        Timelog.Viewer.ReportingClient.SetFilter(currentFilter);
    }
    else if (readConsole == "search")
    {
        currentFilter.StateCode = (int)FilterCriteriaState.Search;
        Timelog.Viewer.ReportingClient.SetFilter(currentFilter);
    }
    else if (readConsole.Contains(" "))
    {
        var parts = readConsole.Split(' ');
        if (parts.Length == 2)
        {
            var field = parts[0];
            var value = parts[1];
            HelperViewer.SetFilter(currentFilter, field, value);
        }
    }

    else if (readConsole.ToLower().Trim() == "help" || readConsole.ToLower().Trim() == "h") { HelperViewer.WriteInstructions(); }
} while (readConsole != ""); //Empty string will terminate the program

//Guid filterA = Guid.Parse("33354290-b0a6-492b-8e97-aa84492d1c7e");
//Guid filterB = Guid.Parse("1cfa7816-5d2f-4ea1-a2d9-dabf09f37ba0");
//Guid filterC = Guid.Parse("d3e3e3e3-5d2f-4ea1-a2d9-dabf09f37ba0");

//var readConsole = "";
//do
//{
//    readConsole = Console.ReadLine();
//    if (readConsole == "1") { HelperViewer.SetFilter(applicationKey, filterA, false); }
//    else if (readConsole == "2") { HelperViewer.SetFilter(applicationKey, filterB, false); }
//    else if (readConsole == "3") { HelperViewer.SetFilter(applicationKey, filterC, false); }
//    else if (readConsole == "4") { HelperViewer.SetFilter(applicationKey, filterA, true); }
//    else if (readConsole == "5") { HelperViewer.SetFilter(applicationKey, filterB, true); }
//    else if (readConsole == "6") { HelperViewer.SetFilter(applicationKey, filterC, true); }
//    else if (readConsole.ToLower().Trim() == "help" || readConsole.ToLower().Trim() == "h") { HelperViewer.WriteInstructions(); }
//} while (readConsole != ""); //Empty string will terminate the program

logger.LogInformation("Terminated. ");

/******************************************************************/
//Just for nice Log Structure
class MainTimelogViewer { }

static class HelperViewer
{
    public static void WriteInstructions()
    {
        Console.Clear();
        Console.WriteLine("Write Command to Send to Server");
        Console.WriteLine("show = Show current filter");
        Console.WriteLine("<field> <value> = set filter field value");
        Console.WriteLine("start = starts real time logging");
        Console.WriteLine("stop = stops real time logging");
        Console.WriteLine("search = performs search logging");
        Console.WriteLine("help or h = Clears console and writes this instructions again");
        
        Console.WriteLine("Press Enter to send...");
        Console.WriteLine("An empty string will terminate the program.");
        Console.WriteLine();
        Console.Write("Valid fields to be set are: (ApplicationKey, DomainMask, MaxLogLevelClient, TransactionID, CommandMask, BeginServerTimestamp, EndServerTimestamp)");
        Console.WriteLine();
    }

    public static void SetFilter(FilterCriteria filter, string field, string value)
    {
        switch (field.ToLower().Trim())
        {
            case "applicationkey":
                filter.ApplicationKey = Guid.Parse(value);
                break;
            case "domainmask":
                filter.DomainMask = int.Parse(value);
                break;
            case "maxloglevelclient":
                filter.MaxLogLevelClient = int.Parse(value);
                break;
            case "transactionid":
                filter.TransactionID = Guid.Parse(value);
                break;
            case "commandmask":
                var candidateCommand = Enum.Parse(typeof(Commands), value, true);
                if (candidateCommand != null) { filter.CommandMask = (Commands)candidateCommand; }
                else if(int.TryParse(value, out int commandMask)) { filter.CommandMask = (Commands)commandMask; }
                break;
            case "beginservertimestamp":
                filter.BeginServerTimestamp = DateTime.Parse(value);
                break;
            case "endservertimestamp":
                filter.EndServerTimestamp = DateTime.Parse(value);
                break;
        }
    }

    public static void ShowFilter(FilterCriteria filter)
    {
        Console.WriteLine($"Viewer: {filter.ViewerGuid}\r\nState: {filter.StateCode}\r\nApplicationKey:{filter.ApplicationKey}\r\nDomain:{filter.DomainMask}\r\nMaxLogLevelClient:{filter.MaxLogLevelClient}\r\nTransaction:{filter.TransactionID}\r\nCommand:{filter.CommandMask}\r\nBeginServerTimestamp:{filter.BeginServerTimestamp}\r\nEndServerTimestamp:{filter.EndServerTimestamp}");
    }


    /// <summary>
    /// Instructions for old way to interact with the program with pre made filters
    /// </summary>
    public static void WriteInstructions_OLD()
    {
        Console.Clear();

        Console.WriteLine("Write Command to Send to Server");
        Console.WriteLine("1 = Set Filter A - matches application with lot's of logs");
        Console.WriteLine("2 = Set Filter B - matches application with fewer number of logs");
        Console.WriteLine("3 = Set Filter C - does not match");

        Console.WriteLine("4 = Search Filter A - matches application with lot's of logs");
        Console.WriteLine("5 = Search Filter B - matches application with fewer number of logs");
        Console.WriteLine("6 = Search Filter C - does not match");

        Console.WriteLine("help or h = Clears console and writes this instructions again");

        Console.WriteLine("Press Enter to send...");
        Console.WriteLine("An empty string will terminate the program.");
    }

    public static void SetFilter_OLD(Guid applicationGuid, Guid filterApplicationGuid, bool searchMode)
    {
        FilterCriteria filterCriteria = new FilterCriteria()
        {
            ViewerGuid = applicationGuid,
            ApplicationKey = filterApplicationGuid,
            StateCode = searchMode ? (int)FilterCriteriaState.Search : (int)FilterCriteriaState.On,
            DomainMask = null,
            MaxLogLevelClient = null,
            TransactionID = null,
            CommandMask = null,
            BeginServerTimestamp = null,
            EndServerTimestamp = null
        };
        Timelog.Viewer.ReportingClient.SetFilter(filterCriteria);
    }

    public static void GetFilter()
    {
        Timelog.Viewer.ReportingClient.GetFilter();
    }

    public static void OnLogMessagesReceived(List<LogMessage> messages)
    {
        foreach(var message in messages)
        {
            Console.WriteLine($"{message.ApplicationKey} | {message.Domain} | {message.ClientLogLevel} | {message.ClientTag} | {message.TransactionID} | {message.Command} | {message.OriginTimestamp} | {message.TimeServerTimeStamp} | {message.ExecutionTime} | {message.Reserved} | {message.MessageHeader} | {message.MessageData}");
        }
    }
}