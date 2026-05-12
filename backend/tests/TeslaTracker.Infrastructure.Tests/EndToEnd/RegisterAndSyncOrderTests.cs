using Azure.Data.Tables;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TeslaTracker.Application.Orders.Commands.RegisterOrderTracking;
using TeslaTracker.Application.Orders.Commands.SyncOrderWithTesla;
using TeslaTracker.Infrastructure.Storage;
using TeslaTracker.Infrastructure.Storage.Entities;
using TeslaTracker.Infrastructure.Tests.TestSupport;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace TeslaTracker.Infrastructure.Tests.EndToEnd;

[Collection("Azurite")]
public sealed class RegisterAndSyncOrderTests : IClassFixture<AzuriteFixture>, IDisposable
{
    private readonly AzuriteFixture _azurite;
    private readonly WireMockServer _wireMock;
    private readonly ServiceProvider _provider;

    public RegisterAndSyncOrderTests(AzuriteFixture azurite)
    {
        _azurite = azurite;
        _wireMock = WireMockServer.Start();
        _provider = TestServiceFactory.Build(new Uri(_wireMock.Url!));
    }

    public void Dispose()
    {
        _provider.Dispose();
        _wireMock.Dispose();
    }

    private void StubTokenRefresh(string newRefreshToken = "rotated-token") =>
        _wireMock
            .Given(Request.Create().WithPath("/oauth2/v3/token").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithBodyAsJson(new
            {
                access_token = "access-abc",
                refresh_token = newRefreshToken,
                expires_in = 28800,
            }));

    private void StubOrders(object response) =>
        _wireMock
            .Given(Request.Create().WithPath("/api/1/users/orders").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBodyAsJson(response));

    private async Task<int> CountHistoryRowsAsync(string orderId)
    {
        var table = _azurite.ServiceClient.GetTableClient(TeslaTrackerTables.OrderEventHistory);
        var count = 0;
        await foreach (var _ in table.QueryAsync<OrderEventHistoryEntity>(e => e.PartitionKey == orderId))
        {
            count++;
        }
        return count;
    }

    private async Task<IReadOnlyList<OrderEventHistoryEntity>> GetHistoryAsync(string orderId)
    {
        var table = _azurite.ServiceClient.GetTableClient(TeslaTrackerTables.OrderEventHistory);
        var rows = new List<OrderEventHistoryEntity>();
        await foreach (var entity in table.QueryAsync<OrderEventHistoryEntity>(e => e.PartitionKey == orderId))
        {
            rows.Add(entity);
        }
        return rows;
    }

    [RequiresAzuriteFact]
    public async Task Registration_Persists_Order_And_Produces_No_History()
    {
        const string orderId = "RN101010101";
        StubTokenRefresh();
        StubOrders(new
        {
            response = new[]
            {
                new
                {
                    referenceNumber = orderId,
                    vin = (string?)null,
                    modelCode = "MY",
                    orderStatus = "ORDER_PLACED",
                    deliveryWindowDisplay = "Q4 2026",
                    deliveryWindowStart = (string?)null,
                    deliveryWindowEnd = (string?)null,
                }
            }
        });

        using var scope = _provider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<RegisterOrderTrackingHandler>();

        var result = await handler.HandleAsync(
            new RegisterOrderTrackingCommand(orderId, "initial-refresh", "1.2.3.4"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(orderId);

        var orderTable = _azurite.ServiceClient.GetTableClient(TeslaTrackerTables.Orders);
        var stored = await orderTable.GetEntityAsync<OrderEntity>(PartitionKeys.Active, orderId);
        stored.Value.IsActive.Should().BeTrue();
        stored.Value.CurrentSnapshotHash.Should().NotBeEmpty();

        var historyCount = await CountHistoryRowsAsync(orderId);
        historyCount.Should().Be(0, "första snapshot vid registrering producerar inga delta-events");
    }

    [RequiresAzuriteFact]
    public async Task Sync_With_Vin_Assignment_Produces_VinAssigned_In_History()
    {
        const string orderId = "RN202020202";
        StubTokenRefresh();
        StubOrders(new
        {
            response = new[]
            {
                new
                {
                    referenceNumber = orderId,
                    vin = (string?)null,
                    modelCode = "MY",
                    orderStatus = "ORDER_PLACED",
                    deliveryWindowDisplay = (string?)null,
                    deliveryWindowStart = (string?)null,
                    deliveryWindowEnd = (string?)null,
                }
            }
        });

        using (var registerScope = _provider.CreateScope())
        {
            var register = registerScope.ServiceProvider.GetRequiredService<RegisterOrderTrackingHandler>();
            (await register.HandleAsync(new RegisterOrderTrackingCommand(orderId, "refresh", "1.2.3.4"), CancellationToken.None))
                .IsSuccess.Should().BeTrue();
        }

        _wireMock.Reset();
        StubTokenRefresh("post-vin-token");
        StubOrders(new
        {
            response = new[]
            {
                new
                {
                    referenceNumber = orderId,
                    vin = "5YJYGDEE0LF999999",
                    modelCode = "MY",
                    orderStatus = "IN_PRODUCTION",
                    deliveryWindowDisplay = "December 2026",
                    deliveryWindowStart = (string?)null,
                    deliveryWindowEnd = (string?)null,
                }
            }
        });

        using (var syncScope = _provider.CreateScope())
        {
            var sync = syncScope.ServiceProvider.GetRequiredService<SyncOrderWithTeslaHandler>();
            var syncResult = await sync.HandleAsync(new SyncOrderWithTeslaCommand(orderId), CancellationToken.None);
            syncResult.IsSuccess.Should().BeTrue();
        }

        var history = await GetHistoryAsync(orderId);
        history.Should().NotBeEmpty();
        history.Should().Contain(h => h.EventType == "VinAssigned");
        history.Should().Contain(h => h.EventType == "OrderStateChanged");
    }

    [RequiresAzuriteFact]
    public async Task Sync_With_401_Unauthorized_Archives_Order()
    {
        const string orderId = "RN303030303";
        StubTokenRefresh();
        StubOrders(new
        {
            response = new[]
            {
                new
                {
                    referenceNumber = orderId,
                    vin = (string?)null,
                    modelCode = "MY",
                    orderStatus = "ORDER_PLACED",
                    deliveryWindowDisplay = (string?)null,
                    deliveryWindowStart = (string?)null,
                    deliveryWindowEnd = (string?)null,
                }
            }
        });

        using (var registerScope = _provider.CreateScope())
        {
            var register = registerScope.ServiceProvider.GetRequiredService<RegisterOrderTrackingHandler>();
            (await register.HandleAsync(new RegisterOrderTrackingCommand(orderId, "refresh", "1.2.3.4"), CancellationToken.None))
                .IsSuccess.Should().BeTrue();
        }

        _wireMock.Reset();
        _wireMock
            .Given(Request.Create().WithPath("/oauth2/v3/token").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(401));

        using (var syncScope = _provider.CreateScope())
        {
            var sync = syncScope.ServiceProvider.GetRequiredService<SyncOrderWithTeslaHandler>();
            var syncResult = await sync.HandleAsync(new SyncOrderWithTeslaCommand(orderId), CancellationToken.None);
            syncResult.IsFailure.Should().BeTrue();
            syncResult.Error.Code.Should().Be("Tesla.Unauthorized");
        }

        var orderTable = _azurite.ServiceClient.GetTableClient(TeslaTrackerTables.Orders);
        var stored = await orderTable.GetEntityAsync<OrderEntity>(PartitionKeys.Archived, orderId);
        stored.Value.IsActive.Should().BeFalse();

        var history = await GetHistoryAsync(orderId);
        history.Should().Contain(h => h.EventType == "OrderArchived");
    }
}
