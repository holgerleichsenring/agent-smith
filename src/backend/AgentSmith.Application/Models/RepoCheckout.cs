using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Models;

/// <summary>
/// p0496: the outcome of getting one repo's source into its sandboxes — the checked-out
/// repository, or the reason it could not be used. The reason used to exist only in a log
/// line, so a run that stopped at checkout told the operator "checkout failed" and nothing
/// about what it found.
/// </summary>
/// <param name="Rung">2026-09-13-a284: the feature branch this repository's work was cut
/// from, or null when the ladder fell through to the clone's own base. It travels with the
/// checkout because the pull request is opened three steps later, by a site that has no
/// sandbox to ask — and a base a run cuts from but does not open against shows every
/// predecessor slice's work as this slice's diff.</param>
public sealed record RepoCheckout(Repository? Repository, string? Problem, string? Rung = null)
{
    public static RepoCheckout Ready(Repository repository, string? rung = null) =>
        new(repository, Problem: null, rung);

    public static RepoCheckout Failed(string problem) => new(Repository: null, problem);
}
