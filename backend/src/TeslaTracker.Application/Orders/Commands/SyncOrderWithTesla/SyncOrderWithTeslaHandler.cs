using TeslaTracker.Application.Abstractions;
using TeslaTracker.Application.Tesla;
using TeslaTracker.Application.Tokens;
using TeslaTracker.Domain.Orders;
using TeslaTracker.Domain.SeedWork;

namespace TeslaTracker.Application.Orders.Commands.SyncOrderWithTesla;

public sealed class SyncOrderWithTeslaHandler : ICommandHandler<SyncOrderWithTeslaCommand>
{
    private readonly IOrderRepository _orders;
    private readonly ITeslaOrderGateway _tesla;
    private readonly ITokenProtector _tokenProtector;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public SyncOrderWithTeslaHandler(
        IOrderRepository orders,
        ITeslaOrderGateway tesla,
        ITokenProtector tokenProtector,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _orders = orders;
        _tesla = tesla;
        _tokenProtector = tokenProtector;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<Unit>> HandleAsync(SyncOrderWithTeslaCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var orderIdResult = OrderId.Create(command.OrderId);
        if (orderIdResult.IsFailure)
        {
            return Result<Unit>.Failure(orderIdResult.Error);
        }

        var order = await _orders.FindAsync(orderIdResult.Value, cancellationToken);
        if (order is null || !order.IsActive)
        {
            return Result<Unit>.Failure("Sync.OrderNotFound", "Order finns inte eller är arkiverad.");
        }

        var plaintextTokenResult = await _tokenProtector.UnprotectAsync(order.Secret, cancellationToken);
        if (plaintextTokenResult.IsFailure)
        {
            order.MarkTokenRevoked(_clock.UtcNow);
            await _orders.UpdateAsync(order, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<Unit>.Failure(plaintextTokenResult.Error);
        }

        var teslaResult = await _tesla.FetchOrderAsync(
            order.Id,
            new TeslaCredential(plaintextTokenResult.Value),
            cancellationToken);

        if (teslaResult.IsFailure)
        {
            if (teslaResult.Error.Code == "Tesla.Unauthorized")
            {
                order.MarkTokenRevoked(_clock.UtcNow);
            }
            else
            {
                order.RecordSyncFailure(_clock.UtcNow);
            }

            await _orders.UpdateAsync(order, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<Unit>.Failure(teslaResult.Error);
        }

        var rotatedSecret = await _tokenProtector.ProtectAsync(teslaResult.Value.NewRefreshToken, cancellationToken);
        order.RotateSecret(rotatedSecret);
        order.ApplySnapshot(teslaResult.Value.Snapshot, _clock.UtcNow);

        await _orders.UpdateAsync(order, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<Unit>.Success(Unit.Value);
    }
}
