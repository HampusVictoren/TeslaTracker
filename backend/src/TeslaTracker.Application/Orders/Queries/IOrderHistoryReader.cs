using TeslaTracker.Domain.Orders;

namespace TeslaTracker.Application.Orders.Queries;

public sealed record OrderHistoryEntry(
    DateTimeOffset OccurredAt,
    string EventType,
    string PayloadJson);

public interface IOrderHistoryReader
{
    Task<IReadOnlyList<OrderHistoryEntry>> GetTimelineAsync(OrderId orderId, int take, CancellationToken cancellationToken);
}
