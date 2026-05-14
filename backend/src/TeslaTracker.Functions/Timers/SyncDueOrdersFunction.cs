using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using TeslaTracker.Application.Abstractions;
using TeslaTracker.Application.Orders.Commands.SyncOrderWithTesla;
using TeslaTracker.Domain.Orders;
using TeslaTracker.Domain.Orders.Specifications;

namespace TeslaTracker.Functions.Timers;

public sealed class SyncDueOrdersFunction
{
    private readonly IOrderRepository _orders;
    private readonly SyncOrderWithTeslaHandler _handler;
    private readonly IClock _clock;
    private readonly ILogger<SyncDueOrdersFunction> _logger;

    public SyncDueOrdersFunction(
        IOrderRepository orders,
        SyncOrderWithTeslaHandler handler,
        IClock clock,
        ILogger<SyncDueOrdersFunction> logger)
    {
        _orders = orders;
        _handler = handler;
        _clock = clock;
        _logger = logger;
    }

    [Function(nameof(SyncDueOrders))]
    public async Task SyncDueOrders(
        [TimerTrigger("0 0 * * * *")] TimerInfo timer,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var spec = new DueForSyncSpec(now);
        var threshold = now - DueForSyncSpec.SyncInterval;

        var processed = 0;
        var failed = 0;

        await foreach (var order in _orders.FindActiveDueForSyncAsync(threshold, cancellationToken))
        {
            if (!spec.IsSatisfiedBy(order))
            {
                continue;
            }

            var result = await _handler.HandleAsync(new SyncOrderWithTeslaCommand(order.Id.Value), cancellationToken);
            processed++;
            if (result.IsFailure)
            {
                failed++;
                _logger.LogWarning("Sync failed for {OrderId}: {Code} {Message}", order.Id, result.Error.Code, result.Error.Message);
            }
        }

        _logger.LogInformation("SyncDueOrders processed {Processed}, failed {Failed}", processed, failed);
    }
}
