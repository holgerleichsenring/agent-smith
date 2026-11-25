using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Builders;

/// <summary>
/// 2026-11-17-023fa: builder for CompletePullRequestsContext. Tolerates a run that never checked out — the handler
/// then has nothing gate-passed to merge and says so;
/// 2026-11-17-3435b: a merge-green run, which checks nothing out, merges against the project's repositories.
/// 2026-11-23-97c2a: the run's ticket key, spelled as the requirement rows spell it, so the merge reads its requirements.
/// 2026-11-24-f9c5c: a run whose pull requests target a series branch — a code run stamped with it, or a merge-green request
/// naming it — merges under the series policy (merge.series_when, past protection).
/// 2026-11-25-9992e: a merge-green request naming a protection merges under it; the project and ticket id let the handler ask
/// whether a series holds the ticket.
/// </summary>
public sealed class CompletePullRequestsContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ResolvedProject project, PipelineContext pipeline)
    {
        var green = pipeline.TryGet<CiGreenMergeRequest>(ContextKeys.CiGreenMerge, out var asked) ? asked : null;
        var series = RunParentTicket.Series(pipeline) ?? green?.SeriesBranch;
        var policy = series is null ? project.Merge : project.Merge.IntoSeriesBranch();
        var ticket = pipeline.TryGet<TicketId>(ContextKeys.TicketId, out var id) ? id : null;
        return new CompletePullRequestsContext(
            green?.Protection is { } protection ? policy.WithProtection(protection) : policy,
            pipeline.TryGet<Repository>(ContextKeys.Repository, out var repository) ? repository?.CurrentBranch : null,
            // 2026-11-17-3435b: a merge-green run checks nothing out; its repositories are the project's.
            pipeline.TryGet<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, out var repos) && repos is { Count: > 0 } ? repos : project.Repos,
            pipeline,
            project.Tracker.Name,
            ticket is null ? null : TicketKey.For(project.Tracker.Type.ToString().ToLowerInvariant(), ticket.Value).Value,
            series)
        { Project = project.Name, TicketId = ticket?.Value };
    }
}
