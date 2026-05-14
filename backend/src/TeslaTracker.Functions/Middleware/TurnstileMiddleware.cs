using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using TeslaTracker.Functions.Attributes;
using TeslaTracker.Functions.Turnstile;

namespace TeslaTracker.Functions.Middleware;

internal sealed class TurnstileMiddleware : IFunctionsWorkerMiddleware
{
    public const string TokenHeader = "X-Turnstile-Token";

    private readonly ITurnstileVerifier _verifier;

    public TurnstileMiddleware(ITurnstileVerifier verifier) => _verifier = verifier;

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        if (!FunctionRequires<RequireTurnstileAttribute>(context))
        {
            await next(context);
            return;
        }

        var httpContext = context.GetHttpContext();
        if (httpContext is null)
        {
            await next(context);
            return;
        }

        var token = httpContext.Request.Headers[TokenHeader].ToString();
        var remoteIp = httpContext.Connection.RemoteIpAddress?.ToString();

        if (!await _verifier.VerifyAsync(token, remoteIp, httpContext.RequestAborted))
        {
            await WriteForbiddenAsync(httpContext);
            return;
        }

        await next(context);
    }

    internal static bool FunctionRequires<TAttribute>(FunctionContext context) where TAttribute : Attribute
    {
        var entryPoint = context.FunctionDefinition.EntryPoint;
        var lastDot = entryPoint.LastIndexOf('.');
        if (lastDot < 0) return false;

        var typeName = entryPoint[..lastDot];
        var methodName = entryPoint[(lastDot + 1)..];

        var type = Type.GetType(typeName, throwOnError: false)
            ?? AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType(typeName, throwOnError: false))
                .FirstOrDefault(t => t is not null);

        return type?.GetMethod(methodName)?.GetCustomAttribute(typeof(TAttribute), inherit: false) is not null;
    }

    private static async Task WriteForbiddenAsync(HttpContext httpContext)
    {
        httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
        httpContext.Response.ContentType = "application/problem+json";
        var problem = new
        {
            type = "https://teslatracker.example.com/errors/turnstile-failed",
            title = "Turnstile-verifiering misslyckades",
            status = 403,
        };
        await httpContext.Response.WriteAsync(JsonSerializer.Serialize(problem), httpContext.RequestAborted);
    }
}
