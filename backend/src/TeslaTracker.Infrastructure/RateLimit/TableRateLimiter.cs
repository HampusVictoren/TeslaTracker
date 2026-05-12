using System.Globalization;
using Azure;
using Azure.Data.Tables;
using TeslaTracker.Application.Abstractions;
using TeslaTracker.Infrastructure.Storage;

namespace TeslaTracker.Infrastructure.RateLimit;

internal sealed class TableRateLimiter : IRateLimiter
{
    private readonly TableClient _table;
    private readonly IClock _clock;

    public TableRateLimiter(TableServiceClient service, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(service);
        _table = service.GetTableClient(TeslaTrackerTables.RateLimits);
        _clock = clock;
    }

    public async Task<bool> TryAcquireAsync(string key, int maxPerMinute, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (maxPerMinute <= 0) return false;

        var now = _clock.UtcNow;
        var bucket = now.ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture);

        const int maxAttempts = 5;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            RateLimitEntity entity;
            ETag ifMatch;

            try
            {
                var existing = await _table.GetEntityAsync<RateLimitEntity>(key, bucket, cancellationToken: cancellationToken);
                entity = existing.Value;
                ifMatch = entity.ETag;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                entity = new RateLimitEntity
                {
                    PartitionKey = key,
                    RowKey = bucket,
                    Count = 0,
                    ExpiresAt = now.AddMinutes(10),
                };
                ifMatch = ETag.All;
            }

            if (entity.Count >= maxPerMinute)
            {
                return false;
            }

            entity.Count++;

            try
            {
                if (ifMatch == ETag.All)
                {
                    await _table.AddEntityAsync(entity, cancellationToken);
                }
                else
                {
                    await _table.UpdateEntityAsync(entity, ifMatch, TableUpdateMode.Replace, cancellationToken);
                }
                return true;
            }
            catch (RequestFailedException ex) when (ex.Status == 409 || ex.Status == 412)
            {
                // Concurrent write — retry
            }
        }

        return false;
    }

    private sealed class RateLimitEntity : ITableEntity
    {
        public string PartitionKey { get; set; } = string.Empty;
        public string RowKey { get; set; } = string.Empty;
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        public int Count { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }
    }
}
