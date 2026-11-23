using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Infrastructure.Persistence.Repositories;

namespace AgentSmith.Server.Services.Sweep;

/// <summary>
/// 2026-11-17-3435b: which open pull requests the CI loop reads next — those of merge: on_ci_green projects whose CI is
/// unread, on an older head, none, pending for less than merge.ci_pending_seconds, or a verdict not acted on, and not
/// read for merge.ci_poll_seconds. The cadence the re-read ticks at is the smallest poll of those projects.
/// <para>2026-11-23-849e: every project's open pull requests carry a hold that reads their CI (d60d), so every project is
/// watched while its CI is pending within merge.ci_pending_seconds, at the same poll. Only an on_ci_green project acts on a
/// verdict, so only it keeps unread, moved-head and unacted records due. The loop now ticks at the smallest poll of every
/// project (60 s by default) instead of only while an on_ci_green project is configured.</para>
/// </summary>
public static class CiWatchSelection
{
    public static IReadOnlyList<ResolvedProject> Watched(IEnumerable<ResolvedProject> projects) => [.. projects];

    /// <summary>The tick the CI loop needs; null when no project is configured.</summary>
    public static TimeSpan? Cadence(IReadOnlyList<ResolvedProject> watched) =>
        watched.Count == 0 ? null : TimeSpan.FromSeconds(watched.Min(p => Math.Max(1, p.Merge.CiPollSeconds)));

    public static async Task<IReadOnlyList<PrReviewRecordView>> DueAsync(
        IReadOnlyList<ResolvedProject> watched, PrReviewRecordRepository records, DateTimeOffset now, CancellationToken ct)
    {
        var due = new List<PrReviewRecordView>();
        foreach (var project in watched)
            due.AddRange(await records.CiDueAsync(project.Name,
                (now - TimeSpan.FromSeconds(project.Merge.CiPollSeconds)).UtcTicks,
                (now - TimeSpan.FromSeconds(project.Merge.CiPendingSeconds)).UtcTicks, project.Merge.When == MergeWhen.OnCiGreen, ct));
        return due;
    }
}
