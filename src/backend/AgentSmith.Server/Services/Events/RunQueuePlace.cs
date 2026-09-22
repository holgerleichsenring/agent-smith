using AgentSmith.Application.Services.Lifecycle;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Entities;

namespace AgentSmith.Server.Services.Events;

/// <summary>
/// 2026-09-22-7c41c: the place in the capacity queue a run carries on the surface — the one
/// gate the overview and the run detail SHARE, so one page cannot disagree with the other.
/// <para>
/// A run parked on an operator question keeps its waiting status while its relaunch is merely
/// waiting for a slot: that status is the resume launcher's own gate, and a run repainted as
/// queued would never be launched. What is positively true in exactly that window is that the
/// relaunch is UNDER WAY, and two rows carry it. The capacity-queue entry is keyed by the
/// parked run's own reserved id, so the position lookup already answers for it; and the lease
/// <see cref="ResumeRunLauncher"/> claims BEFORE it hands the job over and BEFORE it removes
/// that entry, so the two overlap with no gap and the assertion is their union. Without the
/// second arm a free-capacity relaunch would read queued for one pump tick and then flip back
/// to needs-you for the longer launch-to-start span — a flip an operator reads as a SECOND
/// question, which is worse than the single wrong stretch it replaces.
/// </para>
/// </summary>
internal static class RunQueuePlace
{
    /// <summary>The status a park leaves behind. Never written on the relaunch path.</summary>
    internal const string WaitingStatus = "waiting_for_input";

    /// <summary>
    /// A relaunch that already holds its lease has no place in line to report — its entry is
    /// gone. Zero says "under way, not in the queue"; queue positions are 1-based, so the two
    /// can never be confused.
    /// </summary>
    internal const int RelaunchUnderWay = 0;

    /// <summary>
    /// The 1-based place a run carries, or null. A PARKED run carries one too: its relaunch is
    /// enqueued under its own reserved id. A park with neither a place nor a claimed lease is
    /// unchanged and still says it needs an answer, which is the safe direction.
    /// </summary>
    internal static int? Of(
        Run run, IReadOnlyDictionary<string, int> positions,
        IReadOnlySet<string>? relaunching = null)
    {
        if (run.Status is "queued" or WaitingStatus && positions.TryGetValue(run.Id, out var place))
            return place;
        return run.Status == WaitingStatus && relaunching?.Contains(run.Id) == true
            ? RelaunchUnderWay
            : null;
    }

    /// <summary>
    /// The parked runs whose relaunch holds a lease although its queue entry is already gone.
    /// <para>
    /// The lease names a TICKET, not a run: it is claimed before the relaunch starts, so its
    /// run id is attached only once the run is running. A lease therefore counts as this run's
    /// relaunch when it names no run yet, or names this one — a duplicate trigger that started
    /// its own run on the same ticket has attached its own id and is excluded. Freshness bounds
    /// the claim to the launch window: past it the reaper releases the lease and the run asks
    /// for an answer again.
    /// </para>
    /// </summary>
    internal static async Task<IReadOnlySet<string>> RelaunchingAsync(
        IActiveRunLease? lease, TimeProvider clock,
        IReadOnlyDictionary<string, int> positions, IEnumerable<Run> runs, CancellationToken ct)
    {
        var relaunching = new HashSet<string>(StringComparer.Ordinal);
        if (lease is null) return relaunching;
        var cutoff = clock.GetUtcNow() - ActiveRunReaper.LeaseFreshFor;
        foreach (var run in runs)
        {
            if (run.Status != WaitingStatus) continue;
            if (positions.ContainsKey(run.Id)) continue; // the queue entry already answers
            if (string.IsNullOrEmpty(run.Project) || string.IsNullOrEmpty(run.TicketId)) continue;
            var held = await lease.GetByTicketAsync(run.Project, new TicketId(run.TicketId), ct);
            if (held is null || held.HeartbeatAt < cutoff) continue;
            if (!string.IsNullOrEmpty(held.RunId) && held.RunId != run.Id) continue;
            relaunching.Add(run.Id);
        }
        return relaunching;
    }
}
