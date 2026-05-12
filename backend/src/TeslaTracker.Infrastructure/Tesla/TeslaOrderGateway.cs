using TeslaTracker.Application.Tesla;
using TeslaTracker.Domain.Orders;
using TeslaTracker.Domain.SeedWork;

namespace TeslaTracker.Infrastructure.Tesla;

internal sealed class TeslaOrderGateway : ITeslaOrderGateway
{
    private readonly TeslaOwnerApiClient _client;
    private readonly TeslaSnapshotTranslator _translator;

    public TeslaOrderGateway(TeslaOwnerApiClient client, TeslaSnapshotTranslator translator)
    {
        _client = client;
        _translator = translator;
    }

    public async Task<Result<TeslaSyncResult>> FetchOrderAsync(
        OrderId orderId,
        TeslaCredential credential,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(orderId);
        ArgumentNullException.ThrowIfNull(credential);

        var tokenResult = await _client.RefreshAsync(credential.RefreshToken, cancellationToken);
        if (tokenResult.IsFailure)
        {
            return Result<TeslaSyncResult>.Failure(tokenResult.Error);
        }

        var ordersResult = await _client.GetOrdersAsync(tokenResult.Value.AccessToken, cancellationToken);
        if (ordersResult.IsFailure)
        {
            return Result<TeslaSyncResult>.Failure(ordersResult.Error);
        }

        var match = ordersResult.Value.FirstOrDefault(o =>
            string.Equals(o.ReferenceNumber, orderId.Value, StringComparison.OrdinalIgnoreCase));

        if (match is null)
        {
            return Result<TeslaSyncResult>.Failure(
                "Tesla.OrderNotFound",
                $"Order {orderId} hittades inte i Tesla-svaret.");
        }

        var snapshot = _translator.Translate(match);
        return Result<TeslaSyncResult>.Success(new TeslaSyncResult(snapshot, tokenResult.Value.RefreshToken));
    }
}
