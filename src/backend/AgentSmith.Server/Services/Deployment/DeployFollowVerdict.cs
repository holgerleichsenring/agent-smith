using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Infrastructure.Persistence.Entities;

namespace AgentSmith.Server.Services.Deployment;

/// <summary>
/// 2026-11-18-3863f: when a deploy record has settled and when it is read next. A pipeline settles once it is no longer
/// pending; a pull request once it is merged or closed and its CI no longer pending. Pending is measured from the
/// delivery: past merge.ci_pending_seconds a run is no longer read for its CI, and a pipeline or an ended pull request
/// stops being followed with that said. An open pull request is read every merge.ci_poll_seconds while its CI runs and
/// every tickets.pr_reread_interval_seconds after, until a person merges or closes it.
/// </summary>
public static class DeployFollowVerdict
{
    /// <summary>Whether <paramref name="record"/> settled, and why following stopped if not on its own.</summary>
    public static (bool Settled, string? Note) Of(DeployRecord record, MergePolicyConfig merge, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(merge);
        var ended = record.PipelineRunId is not null || PrReviewStates.IsEnded(record.PullRequestState ?? string.Empty);
        if (!ended) return (false, null);
        if (record.CiState != CiStates.Pending) return (true, null);
        return PendingTooLong(record, merge, now)
            ? (true, $"still pending after merge.ci_pending_seconds = {merge.CiPendingSeconds} s, so it is no longer followed; see {record.Url}")
            : (false, null);
    }

    /// <summary>2026-11-18-3863h: whether a settled record delivered green — a pipeline run that succeeded, or a pull
    /// request merged with its CI green or with none reporting. GitOps promotion follows the merge, not a rollout.</summary>
    public static bool Succeeded(DeployRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (record.SettledAtTicks is null) return false;
        return record.PipelineRunId is not null
            ? record.CiState == CiStates.Green
            : record.PullRequestState == PrReviewStates.Merged && record.CiState is CiStates.Green or CiStates.None;
    }

    /// <summary>Whether the record is due for a read at <paramref name="now"/>.</summary>
    public static bool IsDue(DeployRecord record, MergePolicyConfig merge, TicketsConfig tickets, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (record.CheckedAtTicks is not { } checkedAt) return true;
        var fast = record.CiState == CiStates.Pending && !PendingTooLong(record, merge, now);
        var every = TimeSpan.FromSeconds(fast ? merge.CiPollSeconds : tickets.PrRereadIntervalSeconds);
        return now - new DateTimeOffset(checkedAt, TimeSpan.Zero) >= every;
    }

    private static bool PendingTooLong(DeployRecord record, MergePolicyConfig merge, DateTimeOffset now) =>
        now - new DateTimeOffset(record.DeliveredAtTicks, TimeSpan.Zero) > TimeSpan.FromSeconds(merge.CiPendingSeconds);
}
