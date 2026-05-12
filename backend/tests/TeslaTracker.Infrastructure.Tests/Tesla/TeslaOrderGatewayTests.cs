using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Options;
using TeslaTracker.Application.Tesla;
using TeslaTracker.Domain.Orders;
using TeslaTracker.Infrastructure.Tesla;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace TeslaTracker.Infrastructure.Tests.Tesla;

public sealed class TeslaOrderGatewayTests : IAsyncLifetime, IDisposable
{
    private WireMockServer _wireMock = null!;
    private TeslaOrderGateway _gateway = null!;
    private HttpClient _http = null!;

    public void Dispose()
    {
        _wireMock?.Dispose();
        _http?.Dispose();
    }

    public Task InitializeAsync()
    {
        _wireMock = WireMockServer.Start();
        var baseUri = new Uri(_wireMock.Url!);
        var options = Options.Create(new TeslaApiOptions
        {
            AuthBaseUrl = baseUri,
            ApiBaseUrl = baseUri,
            ClientId = "test-client",
            Timeout = TimeSpan.FromSeconds(5),
        });

        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var apiClient = new TeslaOwnerApiClient(_http, options);
        _gateway = new TeslaOrderGateway(apiClient, new TeslaSnapshotTranslator());
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _wireMock.Dispose();
        _http.Dispose();
        return Task.CompletedTask;
    }

    private void StubTokenRefresh(string accessToken = "access-abc", string refreshToken = "refresh-xyz")
    {
        _wireMock
            .Given(Request.Create().WithPath("/oauth2/v3/token").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithBodyAsJson(new
            {
                access_token = accessToken,
                refresh_token = refreshToken,
                expires_in = 28800,
            }));
    }

    private void StubOrders(object responseBody, int statusCode = 200)
    {
        _wireMock
            .Given(Request.Create().WithPath("/api/1/users/orders").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(statusCode).WithBodyAsJson(responseBody));
    }

    [Fact]
    public async Task FetchOrder_Returns_Snapshot_And_Rotated_Token_On_Success()
    {
        StubTokenRefresh(refreshToken: "rotated-refresh-token");
        StubOrders(new
        {
            response = new[]
            {
                new
                {
                    referenceNumber = "RN123456789",
                    vin = "5YJYGDEE0LF000001",
                    modelCode = "MY",
                    orderStatus = "IN_PRODUCTION",
                    deliveryWindowDisplay = "Q4 2026",
                    deliveryWindowStart = (string?)null,
                    deliveryWindowEnd = (string?)null,
                }
            }
        });

        var orderId = OrderId.Create("RN123456789").Value;
        var result = await _gateway.FetchOrderAsync(orderId, new TeslaCredential("original-refresh"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.NewRefreshToken.Should().Be("rotated-refresh-token");
        result.Value.Snapshot.Vin.Should().NotBeNull();
        result.Value.Snapshot.Vin!.Value.Should().Be("5YJYGDEE0LF000001");
        result.Value.Snapshot.State.Should().Be(OrderState.InProduction);
    }

    [Fact]
    public async Task FetchOrder_Returns_Unauthorized_When_Token_Refresh_Fails_401()
    {
        _wireMock
            .Given(Request.Create().WithPath("/oauth2/v3/token").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(401));

        var orderId = OrderId.Create("RN123456789").Value;
        var result = await _gateway.FetchOrderAsync(orderId, new TeslaCredential("bad-refresh"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tesla.Unauthorized");
    }

    [Fact]
    public async Task FetchOrder_Returns_RateLimited_When_Tesla_Returns_429()
    {
        StubTokenRefresh();
        _wireMock
            .Given(Request.Create().WithPath("/api/1/users/orders").UsingGet())
            .RespondWith(Response.Create().WithStatusCode((int)HttpStatusCode.TooManyRequests));

        var orderId = OrderId.Create("RN123456789").Value;
        var result = await _gateway.FetchOrderAsync(orderId, new TeslaCredential("refresh"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tesla.RateLimited");
    }

    [Fact]
    public async Task FetchOrder_Returns_Unavailable_On_5xx()
    {
        StubTokenRefresh();
        _wireMock
            .Given(Request.Create().WithPath("/api/1/users/orders").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(503));

        var orderId = OrderId.Create("RN123456789").Value;
        var result = await _gateway.FetchOrderAsync(orderId, new TeslaCredential("refresh"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tesla.Unavailable");
    }

    [Fact]
    public async Task FetchOrder_Returns_OrderNotFound_When_Order_Missing_From_Response()
    {
        StubTokenRefresh();
        StubOrders(new
        {
            response = new[]
            {
                new
                {
                    referenceNumber = "RN999999999",
                    vin = (string?)null,
                    modelCode = "M3",
                    orderStatus = "ORDER_PLACED",
                    deliveryWindowDisplay = (string?)null,
                    deliveryWindowStart = (string?)null,
                    deliveryWindowEnd = (string?)null,
                }
            }
        });

        var orderId = OrderId.Create("RN123456789").Value;
        var result = await _gateway.FetchOrderAsync(orderId, new TeslaCredential("refresh"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tesla.OrderNotFound");
    }

    [Fact]
    public async Task FetchOrder_Returns_Failure_When_RefreshToken_Empty()
    {
        var orderId = OrderId.Create("RN123456789").Value;
        var result = await _gateway.FetchOrderAsync(orderId, new TeslaCredential("   "), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tesla.MissingRefreshToken");
    }
}
