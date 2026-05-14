using TeslaTracker.Domain.Orders;
using TeslaTracker.Domain.SeedWork;

namespace TeslaTracker.Application.Abstractions;

public interface IOwnerAuthorizer
{
    /// <summary>
    /// Verify that the presented view token grants access to the given order id.
    /// On failure returns the same generic error code regardless of cause (unknown id,
    /// wrong token, archived order) to avoid leaking existence to callers.
    /// </summary>
    Task<Result<Order>> AuthorizeAsync(string orderIdRaw, string presentedViewToken, CancellationToken cancellationToken);
}
