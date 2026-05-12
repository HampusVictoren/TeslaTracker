using System.Security.Cryptography;
using Azure.Data.Tables;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TeslaTracker.Application.Abstractions;
using TeslaTracker.Application.Orders.Commands.RegisterOrderTracking;
using TeslaTracker.Application.Orders.Commands.StopTracking;
using TeslaTracker.Application.Orders.Commands.SyncOrderWithTesla;
using TeslaTracker.Application.Tesla;
using TeslaTracker.Application.Tokens;
using TeslaTracker.Domain.Notifications;
using TeslaTracker.Domain.Orders;
using TeslaTracker.Domain.Orders.Events;
using TeslaTracker.Infrastructure.Crypto;
using TeslaTracker.Infrastructure.DomainEvents;
using TeslaTracker.Infrastructure.RateLimit;
using TeslaTracker.Infrastructure.Storage;
using TeslaTracker.Infrastructure.Storage.Projections;
using TeslaTracker.Infrastructure.Tesla;
using TeslaTracker.Infrastructure.Time;

namespace TeslaTracker.Infrastructure.Tests.TestSupport;

internal static class TestServiceFactory
{
    public static ServiceProvider Build(Uri wireMockBaseUrl, string azuriteConnectionString = "UseDevelopmentStorage=true")
    {
        var services = new ServiceCollection();

        services.AddSingleton(new TableServiceClient(azuriteConnectionString));

        var devKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        services.AddOptions<CryptoOptions>().Configure(o => o.DevKey = devKey);
        services.AddOptions<TeslaApiOptions>().Configure(o =>
        {
            o.AuthBaseUrl = wireMockBaseUrl;
            o.ApiBaseUrl = wireMockBaseUrl;
            o.ClientId = "test-client";
            o.Timeout = TimeSpan.FromSeconds(5);
        });

        services.AddSingleton<Microsoft.Extensions.Logging.ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>), typeof(Microsoft.Extensions.Logging.Abstractions.NullLogger<>));

        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<IAggregateTracker, AggregateTracker>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IDomainEventDispatcher, InMemoryDomainEventDispatcher>();

        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IPushChannelRepository, PushChannelRepository>();
        services.AddScoped<IRateLimiter, TableRateLimiter>();
        services.AddScoped<ITokenProtector, DevelopmentTokenProtector>();

        services.AddSingleton<TeslaSnapshotTranslator>();
        services.AddHttpClient<TeslaOwnerApiClient>(c => c.Timeout = TimeSpan.FromSeconds(5));
        services.AddScoped<ITeslaOrderGateway, TeslaOrderGateway>();

        services.AddScoped<OrderEventHistoryProjection>();
        services.AddScoped<IDomainEventHandler<VinAssigned>>(sp => sp.GetRequiredService<OrderEventHistoryProjection>());
        services.AddScoped<IDomainEventHandler<DeliveryWindowChanged>>(sp => sp.GetRequiredService<OrderEventHistoryProjection>());
        services.AddScoped<IDomainEventHandler<OrderStateChanged>>(sp => sp.GetRequiredService<OrderEventHistoryProjection>());
        services.AddScoped<IDomainEventHandler<OrderArchived>>(sp => sp.GetRequiredService<OrderEventHistoryProjection>());

        services.AddScoped<RegisterOrderTrackingHandler>();
        services.AddScoped<SyncOrderWithTeslaHandler>();
        services.AddScoped<StopTrackingHandler>();

        return services.BuildServiceProvider();
    }
}
