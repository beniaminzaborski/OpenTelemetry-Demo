using Alpha;
using Epsilon.Client;
using MassTransit;
using Microsoft.Extensions.Options;
using Mu.Client;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Refit;
using Shared.MassTransit;
using Swashbuckle.AspNetCore.Annotations;

const string serviceName = "Alpha";
const string serviceVersion = "1.0.0";

var builder = WebApplication.CreateBuilder(args);
var (_, services, configuration, loggingBuilder, _, _) = builder;

services.AddEndpointsApiExplorer()
    .AddSwaggerGen(options =>
    {
        options.CustomSchemaIds(type => type.FullName!.Replace('+', '.'));
        options.DescribeAllParametersInCamelCase();
        options.EnableAnnotations();
    });

services
    .AddRefitClient<IEpsilonClient>()
    .ConfigureHttpClient(c => c.BaseAddress = new Uri(configuration["EpsilonUri"]));

// MassTransit over RabbitMq
services.Configure<MassTransitOptions>(configuration.GetSection(nameof(MassTransitOptions)));

services.AddMassTransit(
    configurator =>
    {
        configurator.SetKebabCaseEndpointNameFormatter();

        configurator.UsingRabbitMq(
            (ctx, cfg) =>
            {
                var options = ctx.GetRequiredService<IOptions<MassTransitOptions>>().Value;

                cfg.Host(
                    options.Host,
                    host =>
                    {
                        host.Username(options.Username);
                        host.Password(options.Password);
                    });

                cfg.ConfigureEndpoints(ctx);
            });
    });

services.AddMuClient();

services.AddOpenTelemetryTracing(
    providerBuilder =>
    {
        providerBuilder
            .AddSource(serviceName)
            .AddSource("MassTransit")
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(serviceName, serviceVersion: serviceVersion))
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddOtlpExporter();
    });

services.AddOpenTelemetryMetrics(
    providerBuilder =>
    {
        providerBuilder
            .AddMeter(serviceName)
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(serviceName, serviceVersion: serviceVersion))
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddOtlpExporter();
    });

loggingBuilder.AddLogsExportWithOpenTelemetry(serviceName, serviceVersion);

var app = builder.Build();

app.UseForwardedPathBase();

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/aggregate", async (IEpsilonClient epsilonClient, IMuClient muClient, ILogger<Program> logger) =>
    {
        logger.LogInformation("Begin aggregate");

        var foo = await epsilonClient.GetFoo();
        var bar = await muClient.GetBar();

        var aggregate = new Aggregate
        {
            FooId = foo.Id,
            FooName = foo.Name,
            BarId = bar.Id,
            BarCost = bar.Cost
        };

        logger.LogInformation("End aggregate");

        return Results.Ok(aggregate);
    })
    .Produces<Aggregate>()
    .WithMetadata(
        new SwaggerOperationAttribute(
            "Aggregate data from Epsilon via Refit and Mu via MassTransit's request/response"));

await app.RunAsync();