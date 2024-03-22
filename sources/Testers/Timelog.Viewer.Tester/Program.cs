using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.Json;
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

HelperViewer.WriteToConsole = configuration.GetValue<bool>("WriteToConsole");
HelperViewer.WriteToFile = configuration.GetValue<bool>("WriteToFile");
HelperViewer.WriteToFilePath = configuration.GetValue<string>("WriteToFilePath");
if(String.IsNullOrWhiteSpace(HelperViewer.WriteToFilePath)) 
{ 
    HelperViewer.WriteToFile = false;
    logger.LogWarning($"Write to file path is on but no File Path was provided. Will not log to file");
}   

//Create directory forHelperViewer.WriteToFilePath if it does not exist
new System.IO.FileInfo(HelperViewer.WriteToFilePath).Directory.Create(); //Will not throw if directory already exists


var filtersFilePath = configuration.GetValue<string>("FiltersFilePath");

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
        Timelog.Viewer.ReportingClient.SetFilter(new List<FilterCriteria>() { currentFilter });
        Console.WriteLine($"Started real-time filtering...");
    }
    else if (readConsole == "stop")
    {
        currentFilter.StateCode = (int)FilterCriteriaState.Paused;
        Timelog.Viewer.ReportingClient.SetFilter(new List<FilterCriteria>() { currentFilter });
        Console.WriteLine($"Stopped real-time filtering");
    }
    else if (readConsole == "search")
    {
        currentFilter.StateCode = (int)FilterCriteriaState.Search;
        Timelog.Viewer.ReportingClient.SetFilter(new List<FilterCriteria>() { currentFilter });
        Console.WriteLine($"Applied Search Filter");
    }
    else if(readConsole == "file")
    {
        if (!File.Exists(filtersFilePath))
        {
            Console.WriteLine($"No file exists at '{filtersFilePath}'");
            continue;
        }

        List<FilterCriteria> filters = new List<FilterCriteria>();
        try
        {
            filters = System.Text.Json.JsonSerializer.Deserialize<List<FilterCriteria>>(File.ReadAllText(filtersFilePath));
        }
        catch(Exception ex)
        {
            Console.WriteLine("Error deserializing filters from file: " + ex.Message);
        }

        if(!filters.Any())
        {
            Console.WriteLine($"No filters found in file '{filtersFilePath}'");
            continue;
        }
        filters.ForEach(f => f.ViewerGuid = applicationKey); //Set the viewer guid for all filters
        Timelog.Viewer.ReportingClient.SetFilter(filters);
    }
    else if (readConsole.Contains("="))
    {
        var parts = readConsole.Split('=');
        var field = parts[0];
        var value = parts[1];
        HelperViewer.SetFilter(currentFilter, field, value);
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
    internal static bool WriteToConsole { get; set; }
    internal static bool WriteToFile { get; set; }
    internal static string WriteToFilePath { get; set; }

    private static object WriteToFileLock = new object();

    public static void WriteInstructions()
    {
        Console.Clear();
        Console.WriteLine("Write Command to Send to Server");
        Console.WriteLine("show = Show current filter");
        Console.WriteLine("<field> = <value> = set filter field value");
        Console.WriteLine("<field> = clear filter field value");
        Console.WriteLine("start = starts real time logging");
        Console.WriteLine("stop = stops real time logging");
        Console.WriteLine("search = performs search logging");
        Console.WriteLine("file = loads file with filters and set them");
        Console.WriteLine("help or h = Clears console and writes this instructions again");
        
        Console.WriteLine("Press Enter to send...");
        Console.WriteLine("An empty string will terminate the program.");
        Console.WriteLine("Valid fields to be set are: (ApplicationKey, BaseDomain, Domain or DomainMask, MaxLogLevelClient, TransactionID, Command or CommandMask, BeginServerTimestamp, EndServerTimestamp)");
        Console.WriteLine();
    }

    public static void SetFilter(FilterCriteria filter, string field, string value)
    {
        switch (field.ToLower().Trim())
        {
            case "applicationkey":
                if (String.IsNullOrWhiteSpace(value)) { filter.ApplicationKey = null; }
                else if(Guid.TryParse(value, out Guid applicationKey))
                { filter.ApplicationKey = applicationKey; }
                Console.WriteLine($"Set ApplicationKey to: {filter.ApplicationKey}");
                break;
            case "basedomain":
                if (String.IsNullOrWhiteSpace(value)) { filter.BaseDomain = null; }
                else if(uint.TryParse(value, out uint baseDomain))
                { filter.BaseDomain = baseDomain; }
                Console.WriteLine($"Set BaseDomain to: {filter.BaseDomain}");
                break;
            case "domain":
            case "domainmask":
                if (String.IsNullOrWhiteSpace(value)) { filter.DomainMask = null; }
                else if(uint.TryParse(value, out uint domainMask))
                { filter.DomainMask = domainMask; }
                Console.WriteLine($"Set Domain Mask to: {filter.DomainMask}");
                break;
            case "maxloglevelclient":
                if (String.IsNullOrWhiteSpace(value)) { filter.MaxLogLevelClient = null; }
                else if(int.TryParse(value, out int maxLogLevelClient))
                { filter.MaxLogLevelClient = maxLogLevelClient; }
                Console.WriteLine($"Set Max Log Level Client to: {filter.MaxLogLevelClient}");
                break;
            case "transactionid":
                if (String.IsNullOrWhiteSpace(value)) { filter.TransactionID = null; }
                else if(Guid.TryParse(value, out Guid transactionID))
                { filter.TransactionID = transactionID; }
                Console.WriteLine($"Set Transaction ID to: {filter.TransactionID}");
                break;
            case "command":
            case "commandmask":
                if(String.IsNullOrWhiteSpace(value)) { filter.CommandMask = null; }
                else if(Enum.TryParse(typeof(Commands), value, true, out object candidateCommand))
                { filter.CommandMask = (Commands)candidateCommand; }
                else if(int.TryParse(value, out int commandMask)) 
                { filter.CommandMask = (Commands)commandMask;  }
                else
                {
                    Console.WriteLine($"Unable to parse and set Command Mask to {value}");
                }
                Console.WriteLine($"Set Command Mask to {filter.CommandMask}");
                break;
            case "beginservertimestamp":
                if (String.IsNullOrWhiteSpace(value)) { filter.BeginServerTimestamp = null; }
                else if (DateTime.TryParse(value, out DateTime beginServerTimestamp))
                { filter.BeginServerTimestamp = beginServerTimestamp; }
                Console.WriteLine($"Set Begin Server Timestamp to {filter.BeginServerTimestamp}");
                break;
            case "endservertimestamp":
                if (String.IsNullOrWhiteSpace(value)) { filter.EndServerTimestamp = null; }
                else if (DateTime.TryParse(value, out DateTime endServerTimestamp))
                { filter.EndServerTimestamp = endServerTimestamp;  }
                Console.WriteLine($"Set End Server Timestamp to {filter.EndServerTimestamp}");
                break;
        }
    }

    public static void ShowFilter(FilterCriteria filter)
    {
        Console.WriteLine($"Viewer: {filter.ViewerGuid}\r\nState: {filter.StateCode}\r\nApplicationKey:{filter.ApplicationKey}\r\nDomain:{filter.DomainMask}\r\nMaxLogLevelClient:{filter.MaxLogLevelClient}\r\nTransaction:{filter.TransactionID}\r\nCommand:{filter.CommandMask}\r\nBeginServerTimestamp:{filter.BeginServerTimestamp}\r\nEndServerTimestamp:{filter.EndServerTimestamp}");
    }

    public static void GetFilter()
    {
        Timelog.Viewer.ReportingClient.GetFilter();
    }

    public static void OnLogMessagesReceived(List<LogMessage> messages)
    {
        if (WriteToConsole)
        {
            WriteLogMessagesToConsole(messages);
        }

        if (WriteToFile)
        {
            WriteLogMessagesToFile(messages);
        }
    }

    private static void WriteLogMessagesToConsole(List<LogMessage> messages)
    {
        foreach (var message in messages)
        {
            Console.WriteLine($"{message.ApplicationKey} | {UIntToIPAddress(message.Domain)}  | {message.ClientLogLevel} | {message.ClientTag} | {message.TransactionID} | {message.Command} | {message.OriginTimestamp} | {message.TimeServerTimeStamp} | {message.ExecutionTime} | {message.Reserved} | {message.MessageHeader} | {message.MessageData}");
        }
    }

    private static void WriteLogMessagesToFile(List<LogMessage> messages)
    {
        var sb = new StringBuilder();
        foreach (var message in messages)
        {
            sb.AppendLine($"{message.ApplicationKey} | {UIntToIPAddress(message.Domain)} | {message.ClientLogLevel} | {message.ClientTag} | {message.TransactionID} | {message.Command} | {message.OriginTimestamp} | {message.TimeServerTimeStamp} | {message.ExecutionTime} | {message.Reserved} | {message.MessageHeader} | {message.MessageData}");
        }

        lock (WriteToFileLock)
        {
            System.IO.File.AppendAllText(WriteToFilePath, sb.ToString());
        }
    }

    public static string UIntToIPAddress(uint ipUint)
    {
        byte[] bytes = BitConverter.GetBytes(ipUint);
        if (BitConverter.IsLittleEndian)
        {
            // Reverse the byte array if the system architecture is little endian
            Array.Reverse(bytes);
        }
        return string.Join(".", bytes);
    }
}