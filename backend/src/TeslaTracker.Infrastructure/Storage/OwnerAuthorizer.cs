using TeslaTracker.Application.Abstractions;
using TeslaTracker.Domain.Orders;
using TeslaTracker.Domain.SeedWork;

namespace TeslaTracker.Infrastructure.Storage;

internal sealed class OwnerAuthorizer : IOwnerAuthorizer
{
    // Single generic error code regardless of cause (unknown id, wrong token, archived).
    // Avoids leaking order existence to callers.
    private static readonly Error Unauthorized = new("Auth.Unauthorized", "Obehörig.");

    private readonly IOrderRepository _orders;

    public OwnerAuthorizer(IOrderRepository orders) => _orders = orders;

    public async Task<Result<Order>> AuthorizeAsync(string orderIdRaw, string presentedViewToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(presentedViewToken))
        {
            return Result<Order>.Failure(Unauthorized);
        }

        var orderIdResult = OrderId.Create(orderIdRaw);
        if (orderIdResult.IsFailure)
        {
            return Result<Order>.Failure(Unauthorized);
        }

        var order = await _orders.FindAsync(orderIdResult.Value, cancellationToken);
        if (order is null)
        {
            return Result<Order>.Failure(Unauthorized);
        }

        if (!order.VerifyViewToken(presentedViewToken))
        {
            return Result<Order>.Failure(Unauthorized);
        }

        return Result<Order>.Success(order);
    }
}
