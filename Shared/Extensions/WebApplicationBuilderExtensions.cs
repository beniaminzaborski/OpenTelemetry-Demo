using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class WebApplicationBuilderExtensions
{
    public static void Deconstruct(
        this WebApplicationBuilder builder,
        out IWebHostEnvironment environment,
        out IServiceCollection services,
        out ConfigurationManager configuration,
        out ILoggingBuilder logging,
        out ConfigureWebHostBuilder webHost,
        out ConfigureHostBuilder host)
    {
        environment = builder.Environment;
        services = builder.Services;
        configuration = builder.Configuration;
        logging = builder.Logging;
        webHost = builder.WebHost;
        host = builder.Host;
    }

    public static ILoggingBuilder AddLogsExportWithOpenTelemetry(this ILoggingBuilder loggingBuilder, string serviceName, string serviceVersion)
    {
        return loggingBuilder.AddOpenTelemetry(cfg =>
        {
            cfg.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(serviceName, serviceVersion: serviceVersion))
                .AddOtlpExporter();

            cfg.IncludeScopes = true;
        });
    }
}