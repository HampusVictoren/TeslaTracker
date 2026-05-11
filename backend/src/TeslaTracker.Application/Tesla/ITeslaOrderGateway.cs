using TeslaTracker.Domain.Orders;
using TeslaTracker.Domain.SeedWork;

namespace TeslaTracker.Application.Tesla;

public sealed record TeslaCredential(string RefreshToken);

public sealed record TeslaSyncResult(OrderSnapshot Snapshot, string NewRefreshToken);

public interface ITeslaOrderGateway
{
    Task<Result<TeslaSyncResult>> FetchOrderAsync(
        OrderId orderId,
        TeslaCredential credential,
        CancellationToken cancellationToken);
}
