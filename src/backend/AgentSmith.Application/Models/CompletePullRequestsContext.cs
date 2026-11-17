using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Models;

/// <summary>
/// 2026-11-17-023fa: context for CompletePullRequestsCommand — the project's merge policy, the run's repos so each
/// pull request resolves its own provider, and the work branch a local repository fast-forwards onto.
/// </summary>
public sealed record CompletePullRequestsContext(
    MergePolicyConfig Policy,
    BranchName? SourceBranch,
    IReadOnlyList<RepoConnection> Configs,
    PipelineContext Pipeline) : ICommandContext;
