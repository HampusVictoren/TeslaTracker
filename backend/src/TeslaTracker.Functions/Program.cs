using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using TeslaTracker.Application.Notifications.Commands.AttachPushChannel;
using TeslaTracker.Application.Notifications.Commands.DetachPushChannel;
using TeslaTracker.Application.Orders.Commands.RegisterOrderTracking;
using TeslaTracker.Application.Orders.Commands.StopTracking;
using TeslaTracker.Application.Orders.Commands.SyncOrderWithTesla;
using TeslaTracker.Application.Orders.Queries.GetOrderStatus;
using TeslaTracker.Application.Orders.Queries.GetOrderTimeline;
using TeslaTracker.Functions.Middleware;
using TeslaTracker.Functions.Turnstile;
using TeslaTracker.Infrastructure;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();
builder.UseMiddleware<SecurityHeadersMiddleware>();
builder.UseMiddleware<ProblemDetailsMiddleware>();
builder.UseMiddleware<TurnstileMiddleware>();

builder.Services.AddOpenTelemetry()
    .UseFunctionsWorkerDefaults()
    .UseAzureMonitorExporter();

var storageConnectionString = builder.Configuration["AzureWebJobsStorage"]
    ?? throw new InvalidOperationException("AzureWebJobsStorage is required.");

builder.Services
    .AddInfrastructure(builder.Configuration)
    .AddDevelopmentTokenProtector()
    .AddPushNotifier()
    .AddTableStorage(storageConnectionString);

builder.Services.AddOptions<TurnstileOptions>()
    .Bind(builder.Configuration.GetSection(TurnstileOptions.SectionName));
builder.Services.AddHttpClient<ITurnstileVerifier, TurnstileVerifier>();

builder.Services.AddScoped<RegisterOrderTrackingHandler>();
builder.Services.AddScoped<SyncOrderWithTeslaHandler>();
builder.Services.AddScoped<StopTrackingHandler>();
builder.Services.AddScoped<GetOrderStatusHandler>();
builder.Services.AddScoped<GetOrderTimelineHandler>();
builder.Services.AddScoped<AttachPushChannelHandler>();
builder.Services.AddScoped<DetachPushChannelHandler>();

builder.Build().Run();

public partial class Program;
