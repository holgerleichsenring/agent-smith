using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0223: surfaces a structured per-repo pull-request outcome to the run detail, so the UI
/// renders "no changes — no PR needed" / a clickable PR link / a real failure reason instead
/// of the raw "git commit · exit 1" sandbox row. p0350: one repo at a time, the moment each
/// outcome is known — batching lost PRs. 2026-10-12-3a36c: its own type, because a kept
/// spec PR is published by <see cref="SpecOnlyPullRequests"/> as well.
/// </summary>
public sealed class PullRequestOutcomePublisher(IEventPublisher events)
{
    public async Task PublishAsync(PipelineContext pipeline, OpenedPullRequest outcome, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(outcome);
        if (!pipeline.TryGet<string>(ContextKeys.RunId, out var runId) || string.IsNullOrEmpty(runId))
            return;
        await events.PublishAsync(
            new PullRequestOutcomeEvent(runId!, outcome.RepoName, PullRequestOutcomeText.Status(outcome.Status),
                DateTimeOffset.UtcNow, outcome.Url, outcome.Reason),
            ct);
    }
}
