namespace TeslaTracker.Functions.Turnstile;

public sealed class TurnstileOptions
{
    public const string SectionName = "Turnstile";

    public string SiteKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public Uri VerifyUrl { get; set; } = new("https://challenges.cloudflare.com/turnstile/v0/siteverify");
}
