using Azure;
using Azure.Data.Tables;

namespace TeslaTracker.Infrastructure.Storage.Entities;

internal sealed class OrderEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public byte[] TrackingSecretCipher { get; set; } = [];
    public string TrackingSecretKeyId { get; set; } = string.Empty;
    public string ViewTokenHash { get; set; } = string.Empty;
    public string ViewTokenSalt { get; set; } = string.Empty;
    public string CurrentSnapshotJson { get; set; } = string.Empty;
    public string CurrentSnapshotHash { get; set; } = string.Empty;
    public DateTimeOffset LastSyncedAt { get; set; }
    public int ConsecutiveFailures { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
