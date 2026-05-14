using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;

namespace TeslaTracker.Functions.Middleware;

internal sealed class ProblemDetailsMiddleware : IFunctionsWorkerMiddleware
{
    private readonly ILogger<ProblemDetailsMiddleware> _logger;

    public ProblemDetailsMiddleware(ILogger<ProblemDetailsMiddleware> logger) => _logger = logger;

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in function {Name}", context.FunctionDefinition.Name);

            var httpContext = context.GetHttpContext();
            if (httpContext is null)
            {
                throw;
            }

            httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
            httpContext.Response.ContentType = "application/problem+json";
            var problem = new
            {
                type = "https://tools.ietf.org/html/rfc9110#section-15.6.1",
                title = "Internal Server Error",
                status = 500,
            };
            await httpContext.Response.WriteAsync(JsonSerializer.Serialize(problem), httpContext.RequestAborted);
        }
    }
}
