using TeslaTracker.Domain.SeedWork;

namespace TeslaTracker.Domain.Orders.Events;

public enum ArchiveReason
{
    Completed,
    TokenRevoked,
    MaxFailuresExceeded,
    UserRequested,
}

public sealed record OrderArchived(OrderId OrderId, ArchiveReason Reason, DateTimeOffset OccurredAt) : IDomainEvent;
