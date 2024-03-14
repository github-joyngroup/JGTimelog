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

    services.AddHostedService<Timelog.Viewer.ViewerClient>();
});

var host = builder.Build();

var logger = host.Services.GetRequiredService<ILogger<MainTimelogViewer>>();
var configuration = host.Services.GetRequiredService<IConfiguration>();

logger.LogInformation("Starting... ");
var timeLogReportingServerDriverConfiguration = configuration.GetSection("TimeLogReporting").Get<ViewerClientConfiguration>();
Timelog.Viewer.ViewerClient.Startup(timeLogReportingServerDriverConfiguration, logger);

logger.LogInformation("Running... ");

var hostTask = host.RunAsync();

Console.WriteLine("Write Command to Send to Server");
Console.WriteLine("1 = Set Filter");
Console.WriteLine("2 = Get Filter");
Console.WriteLine("Press Enter to send...");
Console.WriteLine("An empty string will terminate the program.");

var readConsole = "";
do
{
    readConsole = Console.ReadLine();
    if (readConsole == "1")
    {
        HelperViewer.SetFilter(timeLogReportingServerDriverConfiguration);
        //Timelog.Viewer.TimeLogReportingServerDriver.SendMessage(readConsole);
    }
    else if (readConsole == "2")
    {
        HelperViewer.GetFilter();
    }
} while (readConsole != ""); //Empty string will terminate the program

logger.LogInformation("Terminated. ");

logger.LogInformation("Terminated. ");

/******************************************************************/
//Just for nice Log Structure
class MainTimelogViewer { }

static class HelperViewer
{
    public static void SetFilter(ViewerClientConfiguration viewerClientConfiguration)
    {
        FilterCriteria filterCriteria = new FilterCriteria()
        {
            ViewerGuid = viewerClientConfiguration.WatsonTCPClientConfiguration.ApplicationKey,
            StateCode = (int)FilterCriteriaState.On,
            DomainMask = new byte[] { 192, 168, 1, 1 },
            MaxLogLevelClient = 5,
            TransactionID = null,
            CommandMask = null,
            BeginServerTimestamp = null,
            EndServerTimestamp = null
        };
        Timelog.Viewer.ViewerClient.SetFilter(filterCriteria);
    }

    public static void GetFilter()
    {
        Timelog.Viewer.ViewerClient.GetFilter();
    }
}