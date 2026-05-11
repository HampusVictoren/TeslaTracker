using TeslaTracker.Domain.DomainExceptions;
using TeslaTracker.Domain.Orders.Events;
using TeslaTracker.Domain.SeedWork;

namespace TeslaTracker.Domain.Orders;

public sealed class Order : AggregateRoot
{
    public const int MaxConsecutiveFailures = 5;

    public OrderId Id { get; }
    public TrackingSecret Secret { get; private set; }
    public OrderSnapshot CurrentSnapshot { get; private set; }
    public DateTimeOffset LastSyncedAt { get; private set; }
    public int ConsecutiveFailures { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; }

    private Order(
        OrderId id,
        TrackingSecret secret,
        OrderSnapshot currentSnapshot,
        DateTimeOffset lastSyncedAt,
        int consecutiveFailures,
        bool isActive,
        DateTimeOffset createdAt)
    {
        Id = id;
        Secret = secret;
        CurrentSnapshot = currentSnapshot;
        LastSyncedAt = lastSyncedAt;
        ConsecutiveFailures = consecutiveFailures;
        IsActive = isActive;
        CreatedAt = createdAt;
    }

    public static Order Register(OrderId id, TrackingSecret secret, OrderSnapshot initialSnapshot, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(secret);
        ArgumentNullException.ThrowIfNull(initialSnapshot);

        return new Order(id, secret, initialSnapshot, now, 0, true, now);
    }

    public static Order Rehydrate(
        OrderId id,
        TrackingSecret secret,
        OrderSnapshot currentSnapshot,
        DateTimeOffset lastSyncedAt,
        int consecutiveFailures,
        bool isActive,
        DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(secret);
        ArgumentNullException.ThrowIfNull(currentSnapshot);

        return new Order(id, secret, currentSnapshot, lastSyncedAt, consecutiveFailures, isActive, createdAt);
    }

    public void ApplySnapshot(OrderSnapshot newSnapshot, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(newSnapshot);

        if (!IsActive)
        {
            throw new InvariantViolationException(
                $"Kan inte applicera snapshot på arkiverad order {Id}.");
        }

        ConsecutiveFailures = 0;
        LastSyncedAt = now;

        var previous = CurrentSnapshot;

        if (newSnapshot.RawHash == previous.RawHash)
        {
            return;
        }

        if (newSnapshot.Vin is not null && previous.Vin is null)
        {
            RaiseEvent(new VinAssigned(Id, newSnapshot.Vin, now));
        }

        if (!newSnapshot.DeliveryWindow.Equals(previous.DeliveryWindow))
        {
            RaiseEvent(new DeliveryWindowChanged(Id, previous.DeliveryWindow, newSnapshot.DeliveryWindow, now));
        }

        if (newSnapshot.State != previous.State)
        {
            RaiseEvent(new OrderStateChanged(Id, previous.State, newSnapshot.State, now));
        }

        CurrentSnapshot = newSnapshot;

        if (newSnapshot.State is OrderState.Delivered or OrderState.Canceled)
        {
            ArchiveInternal(
                newSnapshot.State == OrderState.Delivered
                    ? ArchiveReason.Completed
                    : ArchiveReason.UserRequested,
                now);
        }
    }

    public void RecordSyncFailure(DateTimeOffset now)
    {
        if (!IsActive)
        {
            return;
        }

        ConsecutiveFailures++;

        if (ConsecutiveFailures > MaxConsecutiveFailures)
        {
            ArchiveInternal(ArchiveReason.MaxFailuresExceeded, now);
        }
    }

    public void MarkTokenRevoked(DateTimeOffset now) =>
        ArchiveInternal(ArchiveReason.TokenRevoked, now);

    public void Stop(DateTimeOffset now) =>
        ArchiveInternal(ArchiveReason.UserRequested, now);

    public void Reactivate(TrackingSecret newSecret, OrderSnapshot newSnapshot, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(newSecret);
        ArgumentNullException.ThrowIfNull(newSnapshot);

        if (IsActive)
        {
            throw new InvariantViolationException($"Order {Id} är redan aktiv.");
        }

        Secret = newSecret;
        CurrentSnapshot = newSnapshot;
        LastSyncedAt = now;
        ConsecutiveFailures = 0;
        IsActive = true;
    }

    public void RotateSecret(TrackingSecret newSecret)
    {
        ArgumentNullException.ThrowIfNull(newSecret);
        Secret = newSecret;
    }

    private void ArchiveInternal(ArchiveReason reason, DateTimeOffset now)
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        RaiseEvent(new OrderArchived(Id, reason, now));
    }
}
