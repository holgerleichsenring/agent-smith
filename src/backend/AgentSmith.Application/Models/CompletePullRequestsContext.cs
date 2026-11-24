using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Models;

/// <summary>
/// 2026-11-17-023fa: context for CompletePullRequestsCommand — the project's merge policy, the run's repos so each
/// pull request resolves its own provider, and the work branch a local repository fast-forwards onto.
/// 2026-11-23-97c2a: the tracker connection and the run's ticket key, whose requirements must have landed before any merge.
/// 2026-11-24-f9c5c: the series branch the pull requests target, when they do — then <see cref="Policy"/> is the series policy.
/// </summary>
public sealed record CompletePullRequestsContext(
    MergePolicyConfig Policy,
    BranchName? SourceBranch,
    IReadOnlyList<RepoConnection> Configs,
    PipelineContext Pipeline,
    string? Tracker = null,
    string? TicketKey = null,
    string? SeriesBranch = null) : ICommandContext;
