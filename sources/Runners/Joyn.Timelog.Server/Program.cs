using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Joyn.Timelog.Server;
using Joyn.Timelog.Server.Viewers;

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

    services.AddHostedService<Joyn.Timelog.Server.Viewers.ViewersServer>();
    services.AddHostedService<Joyn.Timelog.Server.UDPListener>();
    services.AddHostedService<Joyn.Timelog.Server.LogFileManager>();
});

var host = builder.Build();

var logger = host.Services.GetRequiredService<ILogger<MainTimelogServer>>();
var configuration = host.Services.GetRequiredService<IConfiguration>();

logger.LogInformation("Starting... ");

var applicationKey = configuration.GetValue<Guid>("ApplicationKey");

var udpListenerConfiguration = configuration.GetSection("UDPListener").Get<UDPListenerConfiguration>();
Joyn.Timelog.Server.UDPListener.Startup(udpListenerConfiguration, logger); 

var viewersServerConfiguration = configuration.GetSection("ViewersServer").Get<ViewersServerConfiguration>();
ViewersServer.Startup(viewersServerConfiguration, UDPListener.ReceivedDataQueue, logger);

var logFileManagerConfiguration = configuration.GetSection("LogFileManager").Get<LogFileManagerConfiguration>();
Joyn.Timelog.Server.LogFileManager.Startup(logFileManagerConfiguration, UDPListener.ReceivedDataQueue, logger);

logger.LogInformation("Running... ");

#if DEBUG
var hostTask = host.RunAsync();

//Console.WriteLine("1: List all connected clients");
//Console.WriteLine("2: List all filters");

Console.WriteLine("Press Enter to terminate...");

var readConsole = "";
do
{
    readConsole = Console.ReadLine();
    readConsole = "";//Force termination
} while (readConsole != ""); //Empty string will terminate the program
#else
    host.Run();
#endif

logger.LogInformation("Terminated. ");

/******************************************************************/
//Just for nice Log Structure
class MainTimelogServer { }