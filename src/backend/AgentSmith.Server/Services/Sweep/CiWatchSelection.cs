using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Series;
using AgentSmith.Infrastructure.Persistence.Repositories;

namespace AgentSmith.Server.Services.Sweep;

/// <summary>
/// 2026-11-17-3435b: which open pull requests the CI watch reads next, at the smallest merge.ci_poll_seconds of every project.
/// 2026-11-25-96aea: the server's CI loop is gone; only the tickets an active series holds are read, for their master
/// (<see cref="PrRecordCiDue"/>), and every standalone ticket is a series of one.
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
                (now - 2 * TimeSpan.FromSeconds(project.Merge.CiPendingSeconds)).UtcTicks) // not a limit: past the deadline wake, read once more
                { SeriesTickets = SeriesTickets(activeSeries, project.Name) }, ct));
        return due;
    }

    /// <summary>2026-11-25-9992e: the ticket ids the project's active series hold.</summary>
    public static IReadOnlyCollection<string> SeriesTickets(IReadOnlyList<SeriesRunRecord> activeSeries, string project) =>
        [.. activeSeries.Where(s => s.Project == project).SelectMany(s => s.Tickets.Select(t => t.TicketId)).Distinct(StringComparer.Ordinal)];
}
