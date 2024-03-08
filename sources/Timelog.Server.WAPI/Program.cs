using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using System;
using System.IO;

namespace Timelog.Server.WAPI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var host = CreateWebHostBuilder(args);

            var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            if (env == "Development" || env == "Docker")
            {
                //GenericConfiguration.Startup("appsettings.json", true);

                //if (env == "Development")
                //{
                //    host.UseUrls(SpecificConfigurations.WAPIBaseUrl);
                //}
            }

            host.Build().Run();
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseStartup<Startup>();
    }
}
