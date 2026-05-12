namespace TeslaTracker.Infrastructure.Tesla;

public sealed class TeslaApiOptions
{
    public const string SectionName = "Tesla";

    public Uri AuthBaseUrl { get; set; } = new("https://auth.tesla.com");
    public Uri ApiBaseUrl { get; set; } = new("https://owner-api.teslamotors.com");
    public string ClientId { get; set; } = "ownerapi";
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}
