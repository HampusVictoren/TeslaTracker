using TeslaTracker.Domain.SeedWork;

namespace TeslaTracker.Domain.Orders.Events;

public sealed record DeliveryWindowChanged(
    OrderId OrderId,
    DeliveryWindow PreviousWindow,
    DeliveryWindow NewWindow,
    DateTimeOffset OccurredAt) : IDomainEvent;
