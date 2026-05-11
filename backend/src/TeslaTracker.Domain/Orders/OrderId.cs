using System.Text.RegularExpressions;
using TeslaTracker.Domain.SeedWork;

namespace TeslaTracker.Domain.Orders;

public sealed partial record OrderId
{
    private static readonly Regex RnFormat = RnFormatRegex();

    public string Value { get; }

    private OrderId(string value) => Value = value;

    public static Result<OrderId> Create(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Result<OrderId>.Failure("OrderId.Empty", "Order-ID får inte vara tomt.");
        }

        var trimmed = raw.Trim();
        if (!RnFormat.IsMatch(trimmed))
        {
            return Result<OrderId>.Failure(
                "OrderId.InvalidFormat",
                $"Order-ID '{trimmed}' följer inte formatet RN följt av 9–10 siffror.");
        }

        return new OrderId(trimmed);
    }

    public override string ToString() => Value;

    [GeneratedRegex("^RN[0-9]{9,10}$", RegexOptions.CultureInvariant)]
    private static partial Regex RnFormatRegex();
}
