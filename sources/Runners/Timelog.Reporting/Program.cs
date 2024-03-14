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
using Timelog.Reporting;

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

    services.AddHostedService<Timelog.Reporting.ReportingServer>();
    services.AddHostedService<Timelog.Reporting.ViewerServer>();
});

var host = builder.Build();

var logger = host.Services.GetRequiredService<ILogger<MainTimelogViewer>>();
var configuration = host.Services.GetRequiredService<IConfiguration>();

logger.LogInformation("Starting... ");

var reportingServerConfiguration = configuration.GetSection("ReportingServer").Get<ReportingServerConfiguration>();
Timelog.Reporting.ReportingServer.Startup(reportingServerConfiguration, logger);

var viewerServerConfiguration = configuration.GetSection("ViewerServer").Get<ViewerServerConfiguration>();
Timelog.Reporting.ViewerServer.Startup(viewerServerConfiguration, logger);

logger.LogInformation("Running... ");

var hostTask = host.RunAsync();

Console.WriteLine("Commands:");
Console.WriteLine("1: List all connected clients");
Console.WriteLine("2: List all filters");

Console.WriteLine("Anything else, broadcast to all clients");
Console.WriteLine("Press Enter to send...");
Console.WriteLine("An empty string will terminate the program.");

var readConsole = "";
do
{
    readConsole = Console.ReadLine();
    if (readConsole == "1")
    {
        Console.WriteLine("Listing connected clients:");
        foreach (var client in Timelog.Reporting.ViewerServer.ListClients())
        {
            Console.WriteLine(client);
        }
    }
    else if (readConsole == "2")
    {
        Console.WriteLine("Listing filters:");
        foreach(var filter in Timelog.Reporting.ViewerFiltersHandler.ListFilters())
        {
            Console.WriteLine($"{filter.ViewerGuid}: {filter.State.ToString()} - {filter.Hash}");
        }
        Timelog.Reporting.ViewerFiltersHandler.ListFilters();
    }
    else if (readConsole != "")
    {
        Timelog.Reporting.ViewerServer.BroadcastMessage(readConsole);
    }
} while (readConsole != ""); //Empty string will terminate the program

logger.LogInformation("Terminated. ");

/******************************************************************/
//Just for nice Log Structure
class MainTimelogViewer { }
