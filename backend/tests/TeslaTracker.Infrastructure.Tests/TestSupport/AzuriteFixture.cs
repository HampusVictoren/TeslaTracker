using Azure.Data.Tables;
using TeslaTracker.Infrastructure.Storage;
using Xunit;

namespace TeslaTracker.Infrastructure.Tests.TestSupport;

public sealed class AzuriteFixture : IAsyncLifetime
{
    public const string ConnectionString = "UseDevelopmentStorage=true";

    public TableServiceClient ServiceClient { get; private set; } = null!;
    public bool IsAvailable { get; private set; }

    public async Task InitializeAsync()
    {
        ServiceClient = new TableServiceClient(ConnectionString);

        try
        {
            await foreach (var _ in ServiceClient.QueryAsync(maxPerPage: 1))
            {
                break;
            }
            IsAvailable = true;
        }
        catch
        {
            IsAvailable = false;
            return;
        }

        foreach (var name in TeslaTrackerTables.All)
        {
            await ServiceClient.CreateTableIfNotExistsAsync(name);
        }
    }

    public async Task DisposeAsync()
    {
        if (!IsAvailable) return;

        foreach (var name in TeslaTrackerTables.All)
        {
            var client = ServiceClient.GetTableClient(name);
            await foreach (var entity in client.QueryAsync<TableEntity>(maxPerPage: 100))
            {
                await client.DeleteEntityAsync(entity.PartitionKey, entity.RowKey);
            }
        }
    }
}
