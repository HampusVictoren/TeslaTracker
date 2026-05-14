using Microsoft.AspNetCore.Http;
using TeslaTracker.Application.Abstractions;
using TeslaTracker.Domain.SeedWork;

namespace TeslaTracker.Functions.Http;

internal static class HttpResults
{
    public static IResult Ok<T>(Result<T> result) =>
        result.IsSuccess ? Results.Ok(result.Value) : Map(result.Error);

    public static IResult Created<T>(Result<T> result, string location) =>
        result.IsSuccess ? Results.Created(location, result.Value) : Map(result.Error);

    public static IResult NoContent(Result<Unit> result) =>
        result.IsSuccess ? Results.NoContent() : Map(result.Error);

    public static IResult Map(Error error) => error.Code switch
    {
        "Auth.Unauthorized" => Results.Problem(statusCode: StatusCodes.Status401Unauthorized, type: "https://teslatracker.example.com/errors/unauthorized", title: "Obehörig"),
        "Registration.RateLimited" => Results.Problem(statusCode: StatusCodes.Status429TooManyRequests, type: "https://teslatracker.example.com/errors/rate-limited", title: error.Message),
        "Registration.AlreadyTracked" => Results.Problem(statusCode: StatusCodes.Status409Conflict, type: "https://teslatracker.example.com/errors/conflict", title: error.Message),
        "Registration.MissingToken" => Results.Problem(statusCode: StatusCodes.Status400BadRequest, type: "https://teslatracker.example.com/errors/bad-request", title: error.Message),
        "Tesla.Unauthorized" => Results.Problem(statusCode: StatusCodes.Status400BadRequest, type: "https://teslatracker.example.com/errors/tesla-unauthorized", title: "Tesla-token är ogiltig"),
        "Tesla.RateLimited" => Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, type: "https://teslatracker.example.com/errors/tesla-rate-limited", title: error.Message),
        "Tesla.Unavailable" or "Tesla.Timeout" => Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, type: "https://teslatracker.example.com/errors/tesla-unavailable", title: error.Message),
        "PushChannel.NotFound" or "StopTracking.NotFound" => Results.Problem(statusCode: StatusCodes.Status404NotFound, type: "https://teslatracker.example.com/errors/not-found", title: error.Message),
        var c when c.StartsWith("OrderId.") || c.StartsWith("Vin.") || c.StartsWith("PushEndpoint.") || c.StartsWith("DeliveryWindow.")
            => Results.Problem(statusCode: StatusCodes.Status400BadRequest, type: "https://teslatracker.example.com/errors/validation", title: error.Message, detail: c),
        _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError, type: "https://teslatracker.example.com/errors/internal", title: error.Message),
    };

    public static string? GetBearer(HttpRequest request)
    {
        var auth = request.Headers["Authorization"].ToString();
        const string prefix = "Bearer ";
        return auth.StartsWith(prefix, StringComparison.Ordinal) ? auth[prefix.Length..].Trim() : null;
    }

    public static string GetClientIp(HttpRequest request) =>
        request.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
