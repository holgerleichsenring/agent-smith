using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Models;

/// <summary>
/// p0496: the outcome of getting one repo's source into its sandboxes — the checked-out
/// repository, or the reason it could not be used. The reason used to exist only in a log
/// line, so a run that stopped at checkout told the operator "checkout failed" and nothing
/// about what it found.
/// </summary>
public sealed record RepoCheckout(Repository? Repository, string? Problem)
{
    public static RepoCheckout Ready(Repository repository) => new(repository, Problem: null);

    public static RepoCheckout Failed(string problem) => new(Repository: null, problem);
}
