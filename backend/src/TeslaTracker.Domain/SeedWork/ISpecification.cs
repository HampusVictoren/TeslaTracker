namespace TeslaTracker.Domain.SeedWork;

public interface ISpecification<in T>
{
    bool IsSatisfiedBy(T candidate);
}
