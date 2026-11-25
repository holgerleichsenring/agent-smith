using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Series;
using AgentSmith.Infrastructure.Persistence.Repositories;

namespace AgentSmith.Server.Services.Sweep;

/// <summary>
/// 2026-11-17-3435b: which open pull requests the CI loop reads next — those of review: none projects whose CI is
/// unread, on an older head, none, pending for less than merge.ci_pending_seconds, or a verdict not acted on, and not
/// read for merge.ci_poll_seconds. The cadence the re-read ticks at is the smallest poll of those projects.
/// <para>2026-11-23-849e: every project's open pull requests carry a hold that reads their CI (d60d), so every project is
/// watched while its CI is pending within merge.ci_pending_seconds, at the same poll. Only a review: none project
/// (2026-11-25-9992i) acts on a verdict, so only it keeps unread, moved-head and unacted records due. The loop ticks at the
/// smallest poll of every project (60 s by default).</para>
/// <para>2026-11-25-9992e: the tickets an active series holds are read for their master while their CI waits, in every mode
/// (<see cref="PrRecordCiDue.SeriesTickets"/>).</para>
/// </summary>
public static class CiWatchSelection
{
    public static IReadOnlyList<ResolvedProject> Watched(IEnumerable<ResolvedProject> projects) => [.. projects];

    /// <summary>The tick the CI loop needs; null when no project is configured.</summary>
    public static TimeSpan? Cadence(IReadOnlyList<ResolvedProject> watched) =>
        watched.Count == 0 ? null : TimeSpan.FromSeconds(watched.Min(p => Math.Max(1, p.Merge.CiPollSeconds)));

    public static async Task<IReadOnlyList<PrReviewRecordView>> DueAsync(IReadOnlyList<ResolvedProject> watched,
        PrReviewRecordRepository records, IReadOnlyList<SeriesRunRecord> activeSeries, DateTimeOffset now, CancellationToken ct)
    {
        var due = new List<PrReviewRecordView>();
        foreach (var project in watched)
            due.AddRange(await records.CiDueAsync(project.Name, new PrRecordCiDue(
                (now - TimeSpan.FromSeconds(project.Merge.CiPollSeconds)).UtcTicks,
                (now - TimeSpan.FromSeconds(project.Merge.CiPendingSeconds)).UtcTicks, project.Review == ReviewMode.None)
                { SeriesTickets = SeriesTickets(activeSeries, project.Name) }, ct));
        return due;
    }

    /// <summary>2026-11-25-9992e: the ticket ids the project's active series hold.</summary>
    public static IReadOnlyCollection<string> SeriesTickets(IReadOnlyList<SeriesRunRecord> activeSeries, string project) =>
        [.. activeSeries.Where(s => s.Project == project).SelectMany(s => s.Tickets.Select(t => t.TicketId)).Distinct(StringComparer.Ordinal)];
}
