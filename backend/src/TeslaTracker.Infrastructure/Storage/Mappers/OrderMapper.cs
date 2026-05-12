using TeslaTracker.Domain.Orders;
using TeslaTracker.Infrastructure.Storage.Entities;

namespace TeslaTracker.Infrastructure.Storage.Mappers;

internal static class OrderMapper
{
    public static OrderEntity ToEntity(Order order, string? existingEtag = null)
    {
        ArgumentNullException.ThrowIfNull(order);

        return new OrderEntity
        {
            PartitionKey = order.IsActive ? PartitionKeys.Active : PartitionKeys.Archived,
            RowKey = order.Id.Value,
            TrackingSecretCipher = order.Secret.Cipher.ToArray(),
            TrackingSecretKeyId = order.Secret.KeyId,
            CurrentSnapshotJson = SnapshotJson.Serialize(order.CurrentSnapshot),
            CurrentSnapshotHash = order.CurrentSnapshot.RawHash,
            LastSyncedAt = order.LastSyncedAt,
            ConsecutiveFailures = order.ConsecutiveFailures,
            IsActive = order.IsActive,
            CreatedAt = order.CreatedAt,
            ETag = existingEtag is null ? Azure.ETag.All : new Azure.ETag(existingEtag),
        };
    }

    public static Order ToDomain(OrderEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var orderIdResult = OrderId.Create(entity.RowKey);
        if (orderIdResult.IsFailure)
        {
            throw new InvalidOperationException(
                $"OrderEntity har ogiltigt RowKey '{entity.RowKey}': {orderIdResult.Error.Message}");
        }

        var secretResult = TrackingSecret.Create(entity.TrackingSecretCipher, entity.TrackingSecretKeyId);
        if (secretResult.IsFailure)
        {
            throw new InvalidOperationException(
                $"OrderEntity har trasig TrackingSecret för '{entity.RowKey}': {secretResult.Error.Message}");
        }

        var snapshot = SnapshotJson.Deserialize(entity.CurrentSnapshotJson);

        return Order.Rehydrate(
            orderIdResult.Value,
            secretResult.Value,
            snapshot,
            entity.LastSyncedAt,
            entity.ConsecutiveFailures,
            entity.IsActive,
            entity.CreatedAt);
    }
}
