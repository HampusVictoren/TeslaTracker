using Azure.Data.Tables;
using Xunit;

namespace TeslaTracker.Infrastructure.Tests.TestSupport;

public sealed class RequiresAzuriteFactAttribute : FactAttribute
{
    public RequiresAzuriteFactAttribute()
    {
        if (!IsAzuriteRunning())
        {
            Skip = "Azurite körs inte på localhost:10002. Starta med `azurite` i en separat terminal.";
        }
    }

    private static bool IsAzuriteRunning()
    {
        try
        {
            var service = new TableServiceClient("UseDevelopmentStorage=true");
            var enumerator = service.QueryAsync(maxPerPage: 1).GetAsyncEnumerator();
            try
            {
                enumerator.MoveNextAsync().AsTask().Wait(TimeSpan.FromSeconds(3));
                return true;
            }
            finally
            {
                enumerator.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(3));
            }
        }
        catch
        {
            return false;
        }
    }
}
