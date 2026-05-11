using TeslaTracker.Domain.Orders;

namespace TeslaTracker.Domain.Notifications;

public interface IPushChannelRepository
{
    Task<PushChannel?> FindAsync(OrderId orderId, string endpointHash, CancellationToken cancellationToken);
    IAsyncEnumerable<PushChannel> FindByOrderAsync(OrderId orderId, CancellationToken cancellationToken);
    Task AddAsync(PushChannel channel, CancellationToken cancellationToken);
    Task UpdateAsync(PushChannel channel, CancellationToken cancellationToken);
    Task RemoveAsync(PushChannel channel, CancellationToken cancellationToken);
}
