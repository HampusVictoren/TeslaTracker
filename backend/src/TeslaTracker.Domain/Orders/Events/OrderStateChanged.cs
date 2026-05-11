using TeslaTracker.Domain.SeedWork;

namespace TeslaTracker.Domain.Orders.Events;

public sealed record OrderStateChanged(
    OrderId OrderId,
    OrderState PreviousState,
    OrderState NewState,
    DateTimeOffset OccurredAt) : IDomainEvent;
