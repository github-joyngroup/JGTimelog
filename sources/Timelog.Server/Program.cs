using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Timelog.Server
{
    internal class Program
    {
        static async Task Main2(string[] args)
        {
            //CreateHostBuilder(args, null, null, WireUpWorker).Build().Run();
            //var builder = Host.CreateDefaultBuilder(args);
            //builder.ConfigureAppConfiguration((hostContext, config) =>
            //{
            //    config.AddJsonFile("appsettings.json", optional: false);
            //});

            //builder.ConfigureServices((hostContext, services) =>
            //{
            //    services.AddLogging(logging =>
            //    {
            //        logging.ClearProviders();
            //        logging.AddConsole();
            //    });

            //    services.AddHostedService<Timelog.Server.Listener>();
            //});

            //var host = builder.Build();

            ////get console logger
            ////var logger = host.Services.GetService(typeof(ILogger<Program>)) as ILogger<Program>;
            
            //// Access the IConfiguration service from the host
            //var configuration = host.Services.GetService(typeof(IConfiguration)) as IConfiguration;

            //// Get the TimeLogServer section from appsettings configuration
            //var timeLogServerConfig = configuration.GetSection("TimeLogServer").Get<Configuration>();


            //// Start Timelog server
            //Timelog.Server.Listener.Startup(timeLogServerConfig);

            //// Started up
            //await host.RunAsync();


        }

        private static void WireUpWorker(HostBuilderContext hostContext, IServiceCollection services)
        {
            var configuration = hostContext.Configuration.GetSection("TimeLogServer").Get<Configuration>();
            Timelog.Server.Listener.Startup(configuration);
            services.AddHostedService<Timelog.Server.Listener>();
        }

        private static IHostBuilder CreateHostBuilder(string[] args, Action AfterConfigureConfiguration, Action AfterConfigureLogging, Action<HostBuilderContext, IServiceCollection> AfterConfigureServices)
        {
            var host = Host.CreateDefaultBuilder(args).ConfigureAppConfiguration((hostingContext, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: true);
                config.AddEnvironmentVariables();
                
                AfterConfigureConfiguration?.Invoke();
            })
            .ConfigureLogging((hostingContext, config) =>
            {
                config.AddConsole();
                config.SetMinimumLevel(LogLevel.Debug);

                AfterConfigureLogging?.Invoke();
            })
            .ConfigureServices((hostContext, services) =>
            {
                services.AddOptions();

                AfterConfigureServices?.Invoke(hostContext, services);

            });

            return host;
        }
    }
}
