using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;

namespace Timelog.Server.WAPI
{
    public class Startup
    {
        public IConfiguration Configuration { get; }
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
            //var _configuration = Configuration.GetSection("TimeLogServer").Get<Configuration>();
            //Timelog.Server.Listener.Startup(_configuration);
        }

        

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            //services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_2);
            //services.AddControllers().AddMvcOptions(options => {
            //    options.InputFormatters.Insert(0, new BinaryInputFormatter());
            //    options.OutputFormatters.Insert(0, new BinaryOutputFormatter());
            //});
            services.AddSignalR();
            services.AddHostedService<Timelog.Server.Listener>();
            //services.AddCors(options =>
            //{
            //    string[] origins = SpecificConfigurations.CorsAllowedList.ToArray();
            //    options.AddPolicy("AllOrigins", builder =>
            //    {
            //        builder.WithOrigins(origins)
            //            .AllowAnyHeader()
            //            .AllowAnyMethod()
            //            .AllowCredentials();
            //    });
            //});

            //services.AddSwaggerGen(c =>
            //{
            //    c.SwaggerDoc("v1", new OpenApiInfo { Title = "ApiGateway V2", Version = "v1" });
            //});

            //DocDigitizer.Common.Security.AuthHandshake.DDV2AuthHeader.CurrentAuthHeaderHelper = new AuthServicePeerAuthHelper(SpecificConfigurations.BaseAuthenticationUrl);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            
            //var configPath = Environment.GetEnvironmentVariable("CONFIGS_PATH");
            //loggerFactory.AddLog4Net(Path.Join(String.IsNullOrWhiteSpace(configPath) ? Directory.GetCurrentDirectory() : Path.GetFullPath(configPath), "log4net.config"));
            //log.Info("ApiGatewayV2.Configure Started...");
            //log.Debug("Settings WAPIBaseUrl = " + GenericConfiguration.ReadStringConfiguration("WAPIBaseUrl", "NOT FOUND"));

            //app.UseCors("AllOrigins");
            //app.UseStaticFiles();
            ////app.UseMvc();
            app.UseRouting();
            //app.UseEndpoints(endpoints => { endpoints.MapControllers(); });

            //StartupConfig.RegisterSwagger(app, "ApiGateway V2");

            //log.Info("ApiGatewayV2.Configure Ended...");
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapHub<LogMessageHub>("/logMessageHub");
            });
        }
    }
}
