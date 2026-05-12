namespace TeslaTracker.Infrastructure.Crypto;

public sealed class KeyVaultOptions
{
    public const string SectionName = "KeyVault";

    public Uri? VaultUri { get; set; }
    public string? KeyName { get; set; }
}
