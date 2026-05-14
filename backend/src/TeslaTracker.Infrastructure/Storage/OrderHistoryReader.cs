using Azure.Data.Tables;
using TeslaTracker.Application.Orders.Queries;
using TeslaTracker.Domain.Orders;
using TeslaTracker.Infrastructure.Storage.Entities;

namespace TeslaTracker.Infrastructure.Storage;

internal sealed class OrderHistoryReader : IOrderHistoryReader
{
    private readonly TableClient _table;

    public OrderHistoryReader(TableServiceClient service)
    {
        ArgumentNullException.ThrowIfNull(service);
        _table = service.GetTableClient(TeslaTrackerTables.OrderEventHistory);
    }

    public async Task<IReadOnlyList<OrderHistoryEntry>> GetTimelineAsync(OrderId orderId, int take, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(orderId);

        var entries = new List<OrderHistoryEntry>(take);
        var query = _table.QueryAsync<OrderEventHistoryEntity>(
            filter: e => e.PartitionKey == orderId.Value,
            maxPerPage: take,
            cancellationToken: cancellationToken);

        await foreach (var entity in query)
        {
            entries.Add(new OrderHistoryEntry(entity.OccurredAt, entity.EventType, entity.PayloadJson));
            if (entries.Count >= take) break;
        }

        return entries;
    }
}
