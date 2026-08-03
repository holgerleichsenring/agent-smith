namespace AgentSmith.Contracts.Specs;

/// <summary>
/// p0393a: the pointer half of the split persistence — never the content, which
/// lives in git on the ticket branch.
/// </summary>
public interface ISpecSetPointerStore
{
    Task<SpecSetPointer?> GetAsync(string project, string key, CancellationToken cancellationToken);

    Task SaveAsync(string project, SpecSetPointer pointer, CancellationToken cancellationToken);
}
