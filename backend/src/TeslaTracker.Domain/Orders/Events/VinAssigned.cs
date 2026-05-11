using TeslaTracker.Domain.SeedWork;

namespace TeslaTracker.Domain.Orders.Events;

public sealed record VinAssigned(OrderId OrderId, Vin Vin, DateTimeOffset OccurredAt) : IDomainEvent;
