using System.Text.Json.Serialization;

namespace TeslaTracker.Infrastructure.Tesla.Dto;

internal sealed record TeslaTokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("refresh_token")] string RefreshToken,
    [property: JsonPropertyName("expires_in")] int ExpiresInSeconds);
