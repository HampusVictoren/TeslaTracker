using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace TeslaTracker.Functions.Http;

public sealed class HealthCheckFunction
{
    [Function(nameof(Healthz))]
    public IActionResult Healthz(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "healthz")] HttpRequest req) =>
        new OkObjectResult(new { status = "ok" });
}
