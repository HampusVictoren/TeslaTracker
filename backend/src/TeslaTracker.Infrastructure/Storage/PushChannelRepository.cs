using System.Runtime.CompilerServices;
using Azure;
using Azure.Data.Tables;
using TeslaTracker.Application.Abstractions;
using TeslaTracker.Domain.Notifications;
using TeslaTracker.Domain.Orders;
using TeslaTracker.Infrastructure.Storage.Entities;
using TeslaTracker.Infrastructure.Storage.Mappers;

namespace TeslaTracker.Infrastructure.Storage;

internal sealed class PushChannelRepository : IPushChannelRepository
{
    private readonly TableClient _table;
    private readonly IAggregateTracker _tracker;
    private readonly Dictionary<string, string> _etags = new(StringComparer.OrdinalIgnoreCase);

    public PushChannelRepository(TableServiceClient service, IAggregateTracker tracker)
    {
        ArgumentNullException.ThrowIfNull(service);
        _table = service.GetTableClient(TeslaTrackerTables.PushChannels);
        _tracker = tracker;
    }

    public async Task<PushChannel?> FindAsync(OrderId orderId, string endpointHash, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(orderId);
        try
        {
            var response = await _table.GetEntityAsync<PushChannelEntity>(orderId.Value, endpointHash, cancellationToken: cancellationToken);
            _etags[$"{orderId.Value}:{endpointHash}"] = response.Value.ETag.ToString();
            return PushChannelMapper.ToDomain(response.Value);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async IAsyncEnumerable<PushChannel> FindByOrderAsync(
        OrderId orderId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(orderId);
        var query = _table.QueryAsync<PushChannelEntity>(
            filter: e => e.PartitionKey == orderId.Value,
            cancellationToken: cancellationToken);

        await foreach (var entity in query)
        {
            _etags[$"{orderId.Value}:{entity.RowKey}"] = entity.ETag.ToString();
            yield return PushChannelMapper.ToDomain(entity);
        }
    }

    public async Task AddAsync(PushChannel channel, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(channel);
        var entity = PushChannelMapper.ToEntity(channel);
        var response = await _table.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken);
        if (response.Headers.ETag.HasValue)
        {
            _etags[$"{channel.OrderId.Value}:{channel.EndpointHash}"] = response.Headers.ETag.Value.ToString();
        }
        _tracker.Track(channel);
    }

    public async Task UpdateAsync(PushChannel channel, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(channel);
        var key = $"{channel.OrderId.Value}:{channel.EndpointHash}";
        var existingEtag = _etags.TryGetValue(key, out var etag) ? etag : null;
        var entity = PushChannelMapper.ToEntity(channel, existingEtag);
        var ifMatch = existingEtag is null ? ETag.All : new ETag(existingEtag);
        var response = await _table.UpdateEntityAsync(entity, ifMatch, TableUpdateMode.Replace, cancellationToken);
        if (response.Headers.ETag.HasValue)
        {
            _etags[key] = response.Headers.ETag.Value.ToString();
        }
        _tracker.Track(channel);
    }

    public async Task RemoveAsync(PushChannel channel, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(channel);
        await _table.DeleteEntityAsync(channel.OrderId.Value, channel.EndpointHash, cancellationToken: cancellationToken);
        _etags.Remove($"{channel.OrderId.Value}:{channel.EndpointHash}");
    }
}
