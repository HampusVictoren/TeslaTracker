using TeslaTracker.Domain.Orders;
using TeslaTracker.Domain.SeedWork;

namespace TeslaTracker.Application.Tokens;

public interface ITokenProtector
{
    Task<TrackingSecret> ProtectAsync(string plaintextRefreshToken, CancellationToken cancellationToken);
    Task<Result<string>> UnprotectAsync(TrackingSecret secret, CancellationToken cancellationToken);
}
