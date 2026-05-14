namespace TeslaTracker.Infrastructure.Notifications;

public sealed class NotificationsOptions
{
    public const string SectionName = "Notifications";

    public string? VapidSubject { get; set; }
    public string? VapidPublicKey { get; set; }
    public string? VapidPrivateKey { get; set; }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(VapidSubject) &&
        !string.IsNullOrWhiteSpace(VapidPublicKey) &&
        !string.IsNullOrWhiteSpace(VapidPrivateKey);
}
