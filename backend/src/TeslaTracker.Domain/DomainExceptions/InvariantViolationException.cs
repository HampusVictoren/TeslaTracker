namespace TeslaTracker.Domain.DomainExceptions;

public sealed class InvariantViolationException : Exception
{
    public InvariantViolationException(string message) : base(message)
    {
    }
}
