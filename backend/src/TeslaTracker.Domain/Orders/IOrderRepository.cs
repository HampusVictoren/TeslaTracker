namespace TeslaTracker.Domain.Orders;

public interface IOrderRepository
{
    Task<Order?> FindAsync(OrderId id, CancellationToken cancellationToken);
    Task AddAsync(Order order, CancellationToken cancellationToken);
    Task UpdateAsync(Order order, CancellationToken cancellationToken);
    IAsyncEnumerable<Order> FindActiveDueForSyncAsync(DateTimeOffset olderThan, CancellationToken cancellationToken);
}
