using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Models;

/// <summary>
/// 2026-11-17-023fa: context for CompletePullRequestsCommand — the run's repos so each pull request resolves its own
/// provider, and the work branch a local repository fast-forwards onto.
/// 2026-11-23-97c2a: the tracker connection and the run's ticket key, whose requirements must have landed before any merge.
/// 2026-11-24-f9c5c: the series branch the pull requests target, when they do.
/// 2026-11-25-9992i: the project's <see cref="Review"/> decides whether a standalone ticket merges on CI green, and
/// <see cref="Protection"/> is how a merge asks the host — past protection into a series branch (it carries no host
/// policy) or as a judged merge named, else under the host's branch protection.
/// </summary>
public sealed record CompletePullRequestsContext(
    ReviewMode Review,
    BranchProtection Protection,
    BranchName? SourceBranch,
    IReadOnlyList<RepoConnection> Configs,
    PipelineContext Pipeline,
    string? Tracker = null,
    string? TicketKey = null,
    string? SeriesBranch = null) : ICommandContext
{
    /// <summary>2026-11-25-9992e: the project's name; with <see cref="TicketId"/>, what asks whether a series holds the ticket.</summary>
    public string? Project { get; init; }

    /// <summary>2026-11-25-9992e: the run's ticket id, as the series store names its members.</summary>
    public string? TicketId { get; init; }

    /// <summary>2026-11-25-9992i: squash into a series branch (each ticket one commit there), else the host's own method.</summary>
    public Contracts.Providers.MergeStyle Style { get; init; }
}
