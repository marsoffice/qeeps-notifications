using System;
using System.IO;
using FluentValidation;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SendGrid.Extensions.DependencyInjection;

[assembly: FunctionsStartup(typeof(MarsOffice.Qeeps.Notifications.Startup))]
namespace MarsOffice.Qeeps.Notifications
{
    public class Startup : FunctionsStartup
    {
        public override void ConfigureAppConfiguration(IFunctionsConfigurationBuilder builder)
        {
            FunctionsHostBuilderContext context = builder.GetContext();
            var env = Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT") ?? "Development";
            builder.ConfigurationBuilder
                .AddJsonFile(Path.Combine(context.ApplicationRootPath, "appsettings.json"), optional: true, reloadOnChange: false)
                .AddJsonFile(Path.Combine(context.ApplicationRootPath, $"appsettings.{env}.json"), optional: true, reloadOnChange: false)
                .AddEnvironmentVariables();
        }

        public override void Configure(IFunctionsHostBuilder builder)
        {
            var config = builder.GetContext().Configuration;
            builder.Services.AddValidatorsFromAssembly(typeof(Startup).Assembly);
            builder.Services.AddAutoMapper((svc, cfg) => {
                cfg.AllowNullCollections = true;
            }, typeof(Startup).Assembly);
            builder.Services.AddHttpClient();
            builder.Services.AddSendGrid(options =>
            {
                options.ApiKey = config["sendgridapikey"];
            });
        }
    }
}