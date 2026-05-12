using System.Runtime.CompilerServices;
using Azure;
using Azure.Data.Tables;
using TeslaTracker.Application.Abstractions;
using TeslaTracker.Domain.Orders;
using TeslaTracker.Infrastructure.Storage.Entities;
using TeslaTracker.Infrastructure.Storage.Mappers;

namespace TeslaTracker.Infrastructure.Storage;

internal sealed class OrderRepository : IOrderRepository
{
    private readonly TableClient _table;
    private readonly IAggregateTracker _tracker;
    private readonly Dictionary<string, string> _etags = new(StringComparer.OrdinalIgnoreCase);

    public OrderRepository(TableServiceClient service, IAggregateTracker tracker)
    {
        ArgumentNullException.ThrowIfNull(service);
        _table = service.GetTableClient(TeslaTrackerTables.Orders);
        _tracker = tracker;
    }

    public async Task<Order?> FindAsync(OrderId id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);

        var entity = await TryGetAsync(PartitionKeys.Active, id.Value, cancellationToken)
                  ?? await TryGetAsync(PartitionKeys.Archived, id.Value, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        _etags[id.Value] = entity.ETag.ToString();
        return OrderMapper.ToDomain(entity);
    }

    public async Task AddAsync(Order order, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(order);

        var entity = OrderMapper.ToEntity(order);
        await _table.AddEntityAsync(entity, cancellationToken);
        _etags[order.Id.Value] = entity.ETag.ToString();
        _tracker.Track(order);
    }

    public async Task UpdateAsync(Order order, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(order);

        var partitionShouldBe = order.IsActive ? PartitionKeys.Active : PartitionKeys.Archived;
        var existingEtag = _etags.TryGetValue(order.Id.Value, out var etag) ? etag : null;

        var existingPartition = await ResolvePartitionAsync(order.Id.Value, cancellationToken);

        if (existingPartition is not null && existingPartition != partitionShouldBe)
        {
            await _table.DeleteEntityAsync(existingPartition, order.Id.Value, cancellationToken: cancellationToken);
            var fresh = OrderMapper.ToEntity(order);
            await _table.AddEntityAsync(fresh, cancellationToken);
            _etags[order.Id.Value] = fresh.ETag.ToString();
        }
        else
        {
            var entity = OrderMapper.ToEntity(order, existingEtag);
            var ifMatch = existingEtag is null ? ETag.All : new ETag(existingEtag);
            var response = await _table.UpdateEntityAsync(entity, ifMatch, TableUpdateMode.Replace, cancellationToken);
            if (response.Headers.ETag.HasValue)
            {
                _etags[order.Id.Value] = response.Headers.ETag.Value.ToString();
            }
        }

        _tracker.Track(order);
    }

    public async IAsyncEnumerable<Order> FindActiveDueForSyncAsync(
        DateTimeOffset olderThan,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var query = _table.QueryAsync<OrderEntity>(
            filter: e => e.PartitionKey == PartitionKeys.Active && e.LastSyncedAt <= olderThan,
            cancellationToken: cancellationToken);

        await foreach (var entity in query)
        {
            _etags[entity.RowKey] = entity.ETag.ToString();
            yield return OrderMapper.ToDomain(entity);
        }
    }

    private async Task<OrderEntity?> TryGetAsync(string partitionKey, string rowKey, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _table.GetEntityAsync<OrderEntity>(partitionKey, rowKey, cancellationToken: cancellationToken);
            return response.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    private async Task<string?> ResolvePartitionAsync(string rowKey, CancellationToken cancellationToken)
    {
        if (await TryGetAsync(PartitionKeys.Active, rowKey, cancellationToken) is not null) return PartitionKeys.Active;
        if (await TryGetAsync(PartitionKeys.Archived, rowKey, cancellationToken) is not null) return PartitionKeys.Archived;
        return null;
    }
}
