using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Infrastructure.Persistence.Repositories;

namespace AgentSmith.Server.Services.Sweep;

/// <summary>
/// 2026-11-17-3435b: which open pull requests the CI loop reads next — those of merge: on_ci_green projects whose CI is
/// unread, on an older head, none, pending for less than merge.ci_pending_seconds, or a verdict not acted on, and not
/// read for merge.ci_poll_seconds. The cadence the re-read ticks at is the smallest poll of those projects.
/// </summary>
public static class CiWatchSelection
{
    public static IReadOnlyList<ResolvedProject> Watched(IEnumerable<ResolvedProject> projects) =>
        [.. projects.Where(p => p.Merge.When == MergeWhen.OnCiGreen)];

    /// <summary>The tick the CI loop needs; null when no project waits for CI.</summary>
    public static TimeSpan? Cadence(IReadOnlyList<ResolvedProject> watched) =>
        watched.Count == 0 ? null : TimeSpan.FromSeconds(watched.Min(p => Math.Max(1, p.Merge.CiPollSeconds)));

    public static async Task<IReadOnlyList<PrReviewRecordView>> DueAsync(
        IReadOnlyList<ResolvedProject> watched, PrReviewRecordRepository records, DateTimeOffset now, CancellationToken ct)
    {
        var due = new List<PrReviewRecordView>();
        foreach (var project in watched)
            due.AddRange(await records.CiDueAsync(project.Name,
                (now - TimeSpan.FromSeconds(project.Merge.CiPollSeconds)).UtcTicks,
                (now - TimeSpan.FromSeconds(project.Merge.CiPendingSeconds)).UtcTicks, ct));
        return due;
    }
}
