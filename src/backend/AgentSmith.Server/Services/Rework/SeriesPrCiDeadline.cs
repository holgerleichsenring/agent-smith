using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Series;
using AgentSmith.Infrastructure.Persistence.Entities;

namespace AgentSmith.Server.Services.Rework;

/// <summary>
/// 2026-11-25-96aec: the one stall a pull-request read sees for free — its CI still running merge.ci_pending_seconds after it
/// was first seen running on this head (the stored CI keeps that time while state and head stay). The wake is raised once
/// per pull request and head (<see cref="SeriesWake.Once"/>), so every later read past the deadline adds nothing.
/// </summary>
public static class SeriesPrCiDeadline
{
    public static SeriesWake? Of(PrReviewRecordView? stored, PrReviewRecord read, int pendingSeconds)
    {
        ArgumentNullException.ThrowIfNull(read);
        if (pendingSeconds <= 0 || read.CiState != CiStates.Pending || read.CiHeadSha is not { } head) return null;
        if (stored?.Ci is not { State: CiStates.Pending, At: { } since } was || !was.IsOn(head)) return null;
        var waited = new DateTimeOffset(read.CheckedAtTicks, TimeSpan.Zero) - since;
        if (waited < TimeSpan.FromSeconds(pendingSeconds)) return null;
        return new SeriesWake(SeriesWakeKinds.Ci, $"the CI of pull request {read.Url} on {Short(head)} has run for "
            + $"{(int)waited.TotalMinutes} minutes without a verdict (merge.ci_pending_seconds = {pendingSeconds})")
        { Once = $"ci-deadline:{read.PrKey}:{head}" };
    }

    /// <summary>A head as the wakes name it: its first seven characters.</summary>
    public static string Short(string head)
    {
        ArgumentNullException.ThrowIfNull(head);
        return head[..Math.Min(7, head.Length)];
    }
}
