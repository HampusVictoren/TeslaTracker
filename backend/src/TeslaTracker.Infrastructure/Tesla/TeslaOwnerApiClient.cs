using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using TeslaTracker.Domain.SeedWork;
using TeslaTracker.Infrastructure.Tesla.Dto;

namespace TeslaTracker.Infrastructure.Tesla;

internal sealed class TeslaOwnerApiClient
{
    public const string HttpClientName = "TeslaOwnerApi";

    private readonly HttpClient _http;
    private readonly TeslaApiOptions _options;

    public TeslaOwnerApiClient(HttpClient http, IOptions<TeslaApiOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public async Task<Result<TeslaTokenResponse>> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return Result<TeslaTokenResponse>.Failure("Tesla.MissingRefreshToken", "Refresh token saknas.");
        }

        var url = new Uri(_options.AuthBaseUrl, "/oauth2/v3/token");
        var payload = new
        {
            grant_type = "refresh_token",
            client_id = _options.ClientId,
            refresh_token = refreshToken,
            scope = "openid email offline_access",
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(payload) };
        return await SendAndDeserializeAsync<TeslaTokenResponse>(request, cancellationToken);
    }

    public async Task<Result<IReadOnlyList<TeslaOrderDto>>> GetOrdersAsync(string accessToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return Result<IReadOnlyList<TeslaOrderDto>>.Failure("Tesla.MissingAccessToken", "Access token saknas.");
        }

        var url = new Uri(_options.ApiBaseUrl, "/api/1/users/orders");
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var result = await SendAndDeserializeAsync<TeslaOrderListResponse>(request, cancellationToken);
        return result.IsSuccess
            ? Result<IReadOnlyList<TeslaOrderDto>>.Success(result.Value.Response ?? [])
            : Result<IReadOnlyList<TeslaOrderDto>>.Failure(result.Error);
    }

    private async Task<Result<T>> SendAndDeserializeAsync<T>(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            return Result<T>.Failure("Tesla.Unavailable", $"HTTP-fel mot Tesla: {ex.Message}");
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            return Result<T>.Failure("Tesla.Timeout", $"Timeout mot Tesla: {ex.Message}");
        }

        using (response)
        {
            var errorResult = MapErrorStatus<T>(response.StatusCode);
            if (errorResult is not null)
            {
                return errorResult.Value;
            }

            try
            {
                var deserialized = await response.Content.ReadFromJsonAsync<T>(cancellationToken);
                return deserialized is null
                    ? Result<T>.Failure("Tesla.EmptyResponse", "Tesla returnerade tomt svar.")
                    : Result<T>.Success(deserialized);
            }
            catch (System.Text.Json.JsonException ex)
            {
                return Result<T>.Failure("Tesla.DeserializationFailed", $"Kunde inte tolka Tesla-svaret: {ex.Message}");
            }
        }
    }

    private static Result<T>? MapErrorStatus<T>(HttpStatusCode status) => status switch
    {
        HttpStatusCode.OK => null,
        HttpStatusCode.Unauthorized => Result<T>.Failure("Tesla.Unauthorized", "Refresh token är ogiltig eller har återkallats."),
        HttpStatusCode.Forbidden => Result<T>.Failure("Tesla.Forbidden", "Token saknar behörighet."),
        HttpStatusCode.TooManyRequests => Result<T>.Failure("Tesla.RateLimited", "Tesla rate-limitar oss."),
        HttpStatusCode.NotFound => Result<T>.Failure("Tesla.NotFound", "Tesla-endpoint hittades inte."),
        var s when (int)s >= 500 => Result<T>.Failure("Tesla.Unavailable", $"Tesla returnerade {(int)s}."),
        _ => Result<T>.Failure("Tesla.Unknown", $"Oväntad status {(int)status}."),
    };
}
