using TeslaTracker.Application.Abstractions;
using TeslaTracker.Domain.SeedWork;

namespace TeslaTracker.Application.Orders.Queries.GetOrderStatus;

public sealed class GetOrderStatusHandler : IQueryHandler<GetOrderStatusQuery, OrderStatusDto>
{
    private readonly IOwnerAuthorizer _authorizer;

    public GetOrderStatusHandler(IOwnerAuthorizer authorizer) => _authorizer = authorizer;

    public async Task<Result<OrderStatusDto>> HandleAsync(GetOrderStatusQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var authResult = await _authorizer.AuthorizeAsync(query.OrderId, query.PresentedViewToken, cancellationToken);
        if (authResult.IsFailure)
        {
            return Result<OrderStatusDto>.Failure(authResult.Error);
        }

        var order = authResult.Value;
        var snapshot = order.CurrentSnapshot;
        var dto = new OrderStatusDto(
            order.Id.Value,
            order.IsActive,
            snapshot.VehicleModel,
            snapshot.State,
            snapshot.Vin?.Value,
            snapshot.DeliveryWindow.Start,
            snapshot.DeliveryWindow.End,
            snapshot.DeliveryWindow.DisplayText,
            order.LastSyncedAt,
            order.CreatedAt);

        return Result<OrderStatusDto>.Success(dto);
    }
}
