using AgentSmith.Application.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-11-23-31dca: why a repo's work branch must not be cut from the clone's own base — split out of
/// <see cref="SandboxWorkBranchCheckout"/>. A stacked repo whose prerequisite's branch is not readable would lose that work;
/// 2026-11-24-f9c5b: a series repo whose series branch is still not readable after the publish would send the ticket's pull
/// request to the default branch.
/// </summary>
internal static class WorkBranchBaseStops
{
    /// <summary>The sentence the run stops with; null when the resolved base is the one the run asked for.</summary>
    public static string? Missing(RunBranch requested, RepoConnection config, ResolvedBase resolved)
    {
        if (!resolved.FellThrough) return null;
        if (requested.IsSeriesIn(config.Name))
            return $"'{requested.SeriesBranch}' is the series branch {config.Name} is cut from, but it could not be published or "
                + "read in this clone; cutting from the repository's own base would send this ticket's pull request to the default branch.";
        if (requested.Stacked?.Names(config.Name) != true) return null;
        var parent = requested.ParentFor(config.Name)!;
        return $"'{TicketBranchNamer.Compose(new TicketId(parent)).Value}' is the base of {config.Name} — "
            + $"the branch of #{parent}, an unlanded prerequisite with work there — but it is not readable "
            + "in this clone; cutting from the repository's own base would lose that work.";
    }
}
