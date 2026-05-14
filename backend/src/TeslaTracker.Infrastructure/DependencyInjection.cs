using Azure.Data.Tables;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TeslaTracker.Application.Abstractions;
using TeslaTracker.Application.Notifications.EventHandlers;
using TeslaTracker.Application.Notifications.Ports;
using TeslaTracker.Application.Orders.Queries;
using TeslaTracker.Application.Tesla;
using TeslaTracker.Application.Tokens;
using TeslaTracker.Domain.Notifications;
using TeslaTracker.Domain.Orders;
using TeslaTracker.Domain.Orders.Events;
using TeslaTracker.Infrastructure.Crypto;
using TeslaTracker.Infrastructure.DomainEvents;
using TeslaTracker.Infrastructure.Notifications;
using TeslaTracker.Infrastructure.RateLimit;
using TeslaTracker.Infrastructure.Storage;
using TeslaTracker.Infrastructure.Storage.Projections;
using TeslaTracker.Infrastructure.Tesla;
using TeslaTracker.Infrastructure.Time;

namespace TeslaTracker.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<TeslaApiOptions>().Bind(configuration.GetSection(TeslaApiOptions.SectionName));
        services.AddOptions<CryptoOptions>().Bind(configuration.GetSection(CryptoOptions.SectionName));
        services.AddOptions<KeyVaultOptions>().Bind(configuration.GetSection(KeyVaultOptions.SectionName));
        services.AddOptions<NotificationsOptions>().Bind(configuration.GetSection(NotificationsOptions.SectionName));

        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<IAggregateTracker, AggregateTracker>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IDomainEventDispatcher, InMemoryDomainEventDispatcher>();

        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IPushChannelRepository, PushChannelRepository>();
        services.AddScoped<IRateLimiter, TableRateLimiter>();
        services.AddScoped<IOwnerAuthorizer, OwnerAuthorizer>();
        services.AddScoped<IOrderHistoryReader, OrderHistoryReader>();

        services.AddSingleton<TeslaSnapshotTranslator>();
        services.AddHttpClient<TeslaOwnerApiClient>()
            .AddStandardResilienceHandler(o =>
            {
                o.Retry.MaxRetryAttempts = 3;
                o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
                o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
            });
        services.AddScoped<ITeslaOrderGateway, TeslaOrderGateway>();

        services.AddScoped<OrderEventHistoryProjection>();
        services.AddScoped<IDomainEventHandler<VinAssigned>>(sp => sp.GetRequiredService<OrderEventHistoryProjection>());
        services.AddScoped<IDomainEventHandler<DeliveryWindowChanged>>(sp => sp.GetRequiredService<OrderEventHistoryProjection>());
        services.AddScoped<IDomainEventHandler<OrderStateChanged>>(sp => sp.GetRequiredService<OrderEventHistoryProjection>());
        services.AddScoped<IDomainEventHandler<OrderArchived>>(sp => sp.GetRequiredService<OrderEventHistoryProjection>());

        services.AddScoped<OrderPushDispatcher>();
        services.AddScoped<IDomainEventHandler<VinAssigned>>(sp => sp.GetRequiredService<OrderPushDispatcher>());
        services.AddScoped<IDomainEventHandler<DeliveryWindowChanged>>(sp => sp.GetRequiredService<OrderPushDispatcher>());
        services.AddScoped<IDomainEventHandler<OrderStateChanged>>(sp => sp.GetRequiredService<OrderPushDispatcher>());

        services.AddScoped<IDomainEventHandler<OrderArchived>, OrderArchivedPushCleanup>();

        return services;
    }

    public static IServiceCollection AddDevelopmentTokenProtector(this IServiceCollection services)
    {
        services.AddScoped<ITokenProtector, DevelopmentTokenProtector>();
        return services;
    }

    public static IServiceCollection AddKeyVaultTokenProtector(this IServiceCollection services)
    {
        services.AddScoped<ITokenProtector, KeyVaultTokenProtector>();
        return services;
    }

    public static IServiceCollection AddTableStorage(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        services.AddSingleton(new TableServiceClient(connectionString));
        return services;
    }

    public static IServiceCollection AddPushNotifier(this IServiceCollection services)
    {
        services.AddScoped<IPushNotifier>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<NotificationsOptions>>().Value;
            if (options.IsConfigured)
            {
                return new WebPushNotifier(
                    Options.Create(options),
                    sp.GetRequiredService<ILogger<WebPushNotifier>>());
            }

            var logger = sp.GetRequiredService<ILogger<LoggingPushNotifier>>();
            logger.LogWarning(
                "Notifications:Vapid* not configured — falling back to LoggingPushNotifier. Pushes will be logged, not sent.");
            return new LoggingPushNotifier(logger);
        });
        return services;
    }
}
