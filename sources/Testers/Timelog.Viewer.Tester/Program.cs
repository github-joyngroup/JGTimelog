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
Timelog.Viewer.ReportingClient.Startup(applicationKey, reportingClientConfiguration, logger);

logger.LogInformation("Running... ");

var hostTask = host.RunAsync();

Console.WriteLine("Write Command to Send to Server");
Console.WriteLine("1 = Set Filter A - matches application with lot's of logs");
Console.WriteLine("2 = Set Filter B - matches application with fewer number of logs");
Console.WriteLine("3 = Set Filter C - does not match");

Console.WriteLine("Press Enter to send...");
Console.WriteLine("An empty string will terminate the program.");

Guid filterA = Guid.Parse("33354290-b0a6-492b-8e97-aa84492d1c7e");
Guid filterB = Guid.Parse("1cfa7816-5d2f-4ea1-a2d9-dabf09f37ba0");
Guid filterC = Guid.Parse("d3e3e3e3-5d2f-4ea1-a2d9-dabf09f37ba0");

var readConsole = "";
do
{
    readConsole = Console.ReadLine();
    if (readConsole == "1")
    {
        HelperViewer.SetFilter(applicationKey, filterA);
        //Timelog.Viewer.TimeLogReportingServerDriver.SendMessage(readConsole);
    }
    else if (readConsole == "2")
    {
        HelperViewer.SetFilter(applicationKey, filterB);
    }
    else if (readConsole == "3")
    {
        HelperViewer.SetFilter(applicationKey, filterC);
    }
} while (readConsole != ""); //Empty string will terminate the program

logger.LogInformation("Terminated. ");

logger.LogInformation("Terminated. ");

/******************************************************************/
//Just for nice Log Structure
class MainTimelogViewer { }

static class HelperViewer
{
    public static void SetFilter(Guid applicationGuid, Guid filterApplicationGuid)
    {
        FilterCriteria filterCriteria = new FilterCriteria()
        {
            ViewerGuid = applicationGuid,
            ApplicationKey = filterApplicationGuid,
            StateCode = (int)FilterCriteriaState.On,
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
}