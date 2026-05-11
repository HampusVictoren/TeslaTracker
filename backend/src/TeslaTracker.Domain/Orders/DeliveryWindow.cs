using TeslaTracker.Domain.SeedWork;

namespace TeslaTracker.Domain.Orders;

public sealed record DeliveryWindow
{
    public DateOnly? Start { get; }
    public DateOnly? End { get; }
    public string DisplayText { get; }

    private DeliveryWindow(DateOnly? start, DateOnly? end, string displayText)
    {
        Start = start;
        End = end;
        DisplayText = displayText;
    }

    public static Result<DeliveryWindow> Create(DateOnly? start, DateOnly? end, string? displayText)
    {
        if (start.HasValue && end.HasValue && end.Value < start.Value)
        {
            return Result<DeliveryWindow>.Failure(
                "DeliveryWindow.InvalidRange",
                "Slutdatum får inte vara före startdatum.");
        }

        return new DeliveryWindow(start, end, displayText?.Trim() ?? string.Empty);
    }

    public static DeliveryWindow Unknown() => new(null, null, string.Empty);
}
