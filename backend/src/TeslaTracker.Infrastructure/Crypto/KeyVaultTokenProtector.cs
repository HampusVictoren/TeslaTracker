using TeslaTracker.Application.Tokens;
using TeslaTracker.Domain.Orders;
using TeslaTracker.Domain.SeedWork;

namespace TeslaTracker.Infrastructure.Crypto;

public sealed class KeyVaultTokenProtector : ITokenProtector
{
    public Task<TrackingSecret> ProtectAsync(string plaintextRefreshToken, CancellationToken cancellationToken) =>
        throw new NotImplementedException(
            "KeyVaultTokenProtector implementeras i Sprint 6 (deploy + Bicep). Använd DevelopmentTokenProtector lokalt.");

    public Task<Result<string>> UnprotectAsync(TrackingSecret secret, CancellationToken cancellationToken) =>
        throw new NotImplementedException(
            "KeyVaultTokenProtector implementeras i Sprint 6 (deploy + Bicep). Använd DevelopmentTokenProtector lokalt.");
}
