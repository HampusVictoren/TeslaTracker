using TeslaTracker.Application.Abstractions;
using TeslaTracker.Domain.Orders;
using TeslaTracker.Domain.SeedWork;

namespace TeslaTracker.Application.Orders.Commands.StopTracking;

public sealed class StopTrackingHandler : ICommandHandler<StopTrackingCommand>
{
    private readonly IOrderRepository _orders;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public StopTrackingHandler(IOrderRepository orders, IUnitOfWork unitOfWork, IClock clock)
    {
        _orders = orders;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<Unit>> HandleAsync(StopTrackingCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var orderIdResult = OrderId.Create(command.OrderId);
        if (orderIdResult.IsFailure)
        {
            return Result<Unit>.Failure(orderIdResult.Error);
        }

        var order = await _orders.FindAsync(orderIdResult.Value, cancellationToken);
        if (order is null)
        {
            return Result<Unit>.Failure("StopTracking.NotFound", "Order finns inte.");
        }

        order.Stop(_clock.UtcNow);
        await _orders.UpdateAsync(order, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<Unit>.Success(Unit.Value);
    }
}
