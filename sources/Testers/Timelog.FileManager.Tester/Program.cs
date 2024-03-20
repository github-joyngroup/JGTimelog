using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Timelog.Common;
using Timelog.Common.Models;

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
});

var host = builder.Build();

var logger = host.Services.GetRequiredService<ILogger<MainTimelogViewer>>();
var configuration = host.Services.GetRequiredService<IConfiguration>();

logger.LogInformation("Starting... ");

var applicationKey = configuration.GetValue<Guid>("ApplicationKey");

logger.LogInformation("Running... ");

var hostTask = host.RunAsync();

var files = Directory.GetFiles(@"C:\Temp\Timelog").Select((filePath, idx) => new { Path = filePath, Index = idx}).ToDictionary(pi => pi.Index+1, pi => pi.Path);

HelperViewer.WriteInstructions();
var readConsole = "";
do
{
    readConsole = Console.ReadLine();
    if (readConsole == "0")
    {
        foreach (var file in files)
        {
            Console.WriteLine($"{file.Key}: {file.Value}");
        }
    }
    else if (readConsole.ToLower().Trim() == "help" || readConsole.ToLower().Trim() == "h")
    {
        HelperViewer.WriteInstructions();
    }
    else if (int.TryParse(readConsole, out int fileIndex) && fileIndex > 0 && fileIndex <= files.Count)
    {
        var file = files[fileIndex];
        Console.WriteLine($"Reading file: {file}");
        
        var logEntries = LogMessageFileHandler.ReadLogMessages(file);
        foreach(var message in logEntries)
        {
            Console.WriteLine($"{message.ApplicationKey} | {message.Domain} | {message.ClientLogLevel} | {message.ClientTag} | {message.TransactionID} | {message.Command} | {message.OriginTimestamp} | {message.TimeServerTimeStamp} | {message.Reserved} | {message.MessageHeader} | {message.MessageData}");
        }
    }
    else
    {
        Console.WriteLine("Invalid command");
    }
} while (readConsole != ""); //Empty string will terminate the program

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
        Console.WriteLine("0 = List Files");
        Console.WriteLine("help or h = Clears console and writes this instructions again");
        Console.WriteLine("<n> = read content of files");

        Console.WriteLine("Press Enter to execute...");
        Console.WriteLine("An empty string will terminate the program.");
    }
}