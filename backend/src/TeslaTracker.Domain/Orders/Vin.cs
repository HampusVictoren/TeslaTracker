using System.Text.RegularExpressions;
using TeslaTracker.Domain.SeedWork;

namespace TeslaTracker.Domain.Orders;

public sealed partial record Vin
{
    private static readonly Regex VinFormat = VinFormatRegex();

    public string Value { get; }

    private Vin(string value) => Value = value;

    public static Result<Vin> Create(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Result<Vin>.Failure("Vin.Empty", "VIN får inte vara tomt.");
        }

        var normalized = raw.Trim().ToUpperInvariant();
        if (!VinFormat.IsMatch(normalized))
        {
            return Result<Vin>.Failure(
                "Vin.InvalidFormat",
                $"VIN '{normalized}' följer inte standardformatet (17 tecken, A–Z utom I/O/Q och 0–9).");
        }

        return new Vin(normalized);
    }

    public override string ToString() => Value;

    [GeneratedRegex("^[A-HJ-NPR-Z0-9]{17}$", RegexOptions.CultureInvariant)]
    private static partial Regex VinFormatRegex();
}
