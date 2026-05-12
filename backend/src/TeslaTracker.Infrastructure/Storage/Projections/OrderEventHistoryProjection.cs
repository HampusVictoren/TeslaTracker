using System.Text.Json;
using Azure.Data.Tables;
using TeslaTracker.Application.Abstractions;
using TeslaTracker.Domain.Orders;
using TeslaTracker.Domain.Orders.Events;
using TeslaTracker.Domain.SeedWork;
using TeslaTracker.Infrastructure.Storage.Entities;

namespace TeslaTracker.Infrastructure.Storage.Projections;

internal sealed class OrderEventHistoryProjection :
    IDomainEventHandler<VinAssigned>,
    IDomainEventHandler<DeliveryWindowChanged>,
    IDomainEventHandler<OrderStateChanged>,
    IDomainEventHandler<OrderArchived>
{
    private readonly TableClient _table;

    public OrderEventHistoryProjection(TableServiceClient service)
    {
        ArgumentNullException.ThrowIfNull(service);
        _table = service.GetTableClient(TeslaTrackerTables.OrderEventHistory);
    }

    public Task HandleAsync(VinAssigned domainEvent, CancellationToken cancellationToken) =>
        WriteAsync(domainEvent.OrderId, nameof(VinAssigned), domainEvent, cancellationToken);

    public Task HandleAsync(DeliveryWindowChanged domainEvent, CancellationToken cancellationToken) =>
        WriteAsync(domainEvent.OrderId, nameof(DeliveryWindowChanged), domainEvent, cancellationToken);

    public Task HandleAsync(OrderStateChanged domainEvent, CancellationToken cancellationToken) =>
        WriteAsync(domainEvent.OrderId, nameof(OrderStateChanged), domainEvent, cancellationToken);

    public Task HandleAsync(OrderArchived domainEvent, CancellationToken cancellationToken) =>
        WriteAsync(domainEvent.OrderId, nameof(OrderArchived), domainEvent, cancellationToken);

    private Task WriteAsync(OrderId orderId, string eventType, IDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        var invertedTicks = (DateTimeOffset.MaxValue.Ticks - domainEvent.OccurredAt.Ticks).ToString("D19", System.Globalization.CultureInfo.InvariantCulture);
        var uniqueSuffix = Guid.NewGuid().ToString("N");
        var rowKey = $"{invertedTicks}_{uniqueSuffix}";
        var entity = new OrderEventHistoryEntity
        {
            PartitionKey = orderId.Value,
            RowKey = rowKey,
            EventType = eventType,
            PayloadJson = JsonSerializer.Serialize(domainEvent, domainEvent.GetType()),
            OccurredAt = domainEvent.OccurredAt,
        };

        return _table.AddEntityAsync(entity, cancellationToken);
    }
}
