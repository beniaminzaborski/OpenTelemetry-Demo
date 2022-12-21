using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

const string serviceName = "Omicron";
const string serviceVersion = "1.0.0";

var builder = WebApplication.CreateBuilder(args);
var (environment, services, configuration, loggingBuilder, _, _) = builder;

builder.Configuration
    .AddJsonFile("ocelot.json", false, true)
    .AddJsonFile($"ocelot.{environment.EnvironmentName}.json", true, true);

services.AddOcelot();

services
    .AddEndpointsApiExplorer()
    .AddSwaggerGen();

services.AddSwaggerForOcelot(configuration);

services.AddOpenTelemetryTracing(providerBuilder =>
{
    providerBuilder
        .AddConsoleExporter()
        .AddSource(serviceName)
        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(serviceName, serviceVersion: serviceVersion))
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter();
});

services.AddOpenTelemetryMetrics(
    providerBuilder =>
    {
        providerBuilder
            .AddConsoleExporter()
            .AddMeter(serviceName)
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(serviceName, serviceVersion: serviceVersion))
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
        .AddOtlpExporter();
    });

loggingBuilder.AddLogsExportWithOpenTelemetry(serviceName, serviceVersion);

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerForOcelotUI();

await app.UseOcelot();

await app.RunAsync();