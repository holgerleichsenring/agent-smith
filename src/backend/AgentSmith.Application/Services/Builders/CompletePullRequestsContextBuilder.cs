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
/// naming it — merges past protection: a series branch carries no host policy.
/// 2026-11-25-9992e: a merge-green request naming a protection merges under it; the project and ticket id let the handler ask
/// whether a series holds the ticket. 2026-11-25-9992i: every other merge respects the host's branch protection.
/// </summary>
public sealed class CompletePullRequestsContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ResolvedProject project, PipelineContext pipeline)
    {
        var green = pipeline.TryGet<CiGreenMergeRequest>(ContextKeys.CiGreenMerge, out var asked) ? asked : null;
        var series = RunParentTicket.Series(pipeline) ?? green?.SeriesBranch;
        var ticket = pipeline.TryGet<TicketId>(ContextKeys.TicketId, out var id) ? id : null;
        return new CompletePullRequestsContext(project.Review, Protection(green, series),
            pipeline.TryGet<Repository>(ContextKeys.Repository, out var repository) ? repository?.CurrentBranch : null,
            // 2026-11-17-3435b: a merge-green run checks nothing out; its repositories are the project's.
            pipeline.TryGet<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, out var repos) && repos is { Count: > 0 } ? repos : project.Repos,
            pipeline,
            project.Tracker.Name,
            ticket is null ? null : TicketKey.For(project.Tracker.Type.ToString().ToLowerInvariant(), ticket.Value).Value,
            series)
        { Project = project.Name, TicketId = ticket?.Value, Style = Style(series) };
    }

    /// <summary>2026-11-25-9992i: how a merge asks the host — the judged request's protection, past protection into a series
    /// branch, else under the host's branch protection (a limit of the environment, never a setting).</summary>
    public static BranchProtection Protection(CiGreenMergeRequest? green, string? seriesBranch) =>
        green?.Protection ?? (seriesBranch is null ? BranchProtection.Respect : BranchProtection.Bypass);

    /// <summary>2026-11-25-9992i: a merge into a series branch squashes, so each ticket is one commit there; any other keeps the
    /// repository's own method.</summary>
    public static Contracts.Providers.MergeStyle Style(string? seriesBranch) =>
        seriesBranch is null ? Contracts.Providers.MergeStyle.HostDefault : Contracts.Providers.MergeStyle.Squash;
}
