using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Builders;

/// <summary>
/// 2026-11-17-023fa: builder for CompletePullRequestsContext. Tolerates a run that never checked out — the handler
/// then has nothing gate-passed to merge and says so;
/// 2026-11-17-3435b: a merge-green run, which checks nothing out, merges against the project's repositories.
/// </summary>
public sealed class CompletePullRequestsContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ResolvedProject project, PipelineContext pipeline)
        => new CompletePullRequestsContext(
            project.Merge,
            pipeline.TryGet<Repository>(ContextKeys.Repository, out var repository) ? repository?.CurrentBranch : null,
            // 2026-11-17-3435b: a merge-green run checks nothing out; its repositories are the project's.
            pipeline.TryGet<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, out var repos) && repos is { Count: > 0 } ? repos : project.Repos,
            pipeline);
}
