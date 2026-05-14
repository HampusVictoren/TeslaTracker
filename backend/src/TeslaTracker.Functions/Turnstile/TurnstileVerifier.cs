using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TeslaTracker.Functions.Turnstile;

internal sealed class TurnstileVerifier : ITurnstileVerifier
{
    private readonly HttpClient _http;
    private readonly TurnstileOptions _options;
    private readonly ILogger<TurnstileVerifier> _logger;

    public TurnstileVerifier(HttpClient http, IOptions<TurnstileOptions> options, ILogger<TurnstileVerifier> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<bool> VerifyAsync(string? token, string? remoteIp, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        try
        {
            var payload = new Dictionary<string, string?>
            {
                ["secret"] = _options.SecretKey,
                ["response"] = token,
            };
            if (!string.IsNullOrWhiteSpace(remoteIp))
            {
                payload["remoteip"] = remoteIp;
            }

            using var response = await _http.PostAsync(_options.VerifyUrl, new FormUrlEncodedContent(payload!), cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Turnstile verify returned {Status}", response.StatusCode);
                return false;
            }

            var verifyResponse = await response.Content.ReadFromJsonAsync<TurnstileResponse>(cancellationToken);
            return verifyResponse?.Success ?? false;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Turnstile verify HTTP error");
            return false;
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Turnstile verify timed out");
            return false;
        }
    }

    private sealed record TurnstileResponse([property: JsonPropertyName("success")] bool Success);
}
