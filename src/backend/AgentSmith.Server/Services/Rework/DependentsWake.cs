using AgentSmith.Application.Services.Claim;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Rework;

/// <summary>
/// 2026-10-14-dbceh: a landing wakes the tickets that wait for it — one RequirementsMet nudge per dependent,
/// served under the dependent's lock by <see cref="RequirementsMetStart"/>, which starts it only when the gate
/// would admit it. Called by <see cref="TicketLandingRecorder"/> after a WON insert only, so one replica wakes.
/// <para>A wake lost to a crash or a failed enqueue is recovered, not trusted: the periodic re-read asks
/// <see cref="RecoverAsync"/>, which re-derives it from the requirement and landing rows.</para>
/// </summary>
public sealed class DependentsWake(
    ITicketRequirementStore requirements,
    ITicketLandingStore landings,
    ITicketClosedMarkStore closed, // 2026-10-15-114fa: a closed dependent is not woken
    UnmetTicketRequirements unmet,
    IPreviousAttemptReader attempts,
    IActiveRunLease leases,
    IReworkNudges nudges,
    TimeProvider clock,
    ILogger<DependentsWake> logger)
{
    /// <summary>Nudges every distinct dependent of the landed ticket; answers how many. A failure is logged — the recovery retries it.</summary>
    public async Task<int> WakeAsync(TicketLandingFact landed, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(landed);
        try
        {
            var dependents = (await requirements.DependentsOfAsync(landed.Tracker, landed.TicketKey, ct))
                .DistinctBy(e => e.TicketKey, StringComparer.Ordinal).ToList();
            foreach (var edge in dependents) await NudgeAsync(edge, ct);
            if (dependents.Count > 0)
                logger.LogInformation("The landing of {Project}/{Ticket} woke {Count} waiting ticket(s)", landed.Project, landed.TicketId, dependents.Count);
            return dependents.Count;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Waking the tickets waiting for {Project}/{Ticket} failed; the re-read recovers it", landed.Project, landed.TicketId);
            return 0;
        }
    }

    /// <summary>
    /// Nudges each dependent freed by a landing written within tickets.dependents_wake_window_hours that is ready to start: every
    /// edge StartsWhenMet, every requirement landed, filed completely, not closed, never attempted, not queued, not leased.
    /// </summary>
    /// <param name="tickets">2026-11-17-f9b0d: the tickets: block the caller read this pass, for the window and cap.</param>
    public async Task<int> RecoverAsync(IReadOnlyCollection<string> projects, TicketsConfig tickets, CancellationToken ct)
    {
        var candidates = await landings.DependentsOfLandingsSinceAsync(
            clock.GetUtcNow() - TimeSpan.FromHours(tickets.DependentsWakeWindowHours), projects, tickets.DependentsWakeCap, ct);
        if (candidates.Count >= tickets.DependentsWakeCap)
            logger.LogInformation("The wake recovery read tickets.dependents_wake_cap = {Cap} dependents, its bound per pass; "
                + "the rest are read on the next pass", tickets.DependentsWakeCap);
        var woken = 0;
        foreach (var edge in candidates)
        {
            if (!await ReadyAsync(edge, ct)) continue;
            await NudgeAsync(edge, ct);
            woken++;
        }
        if (woken > 0) logger.LogInformation("Recovered the wake of {Count} waiting ticket(s)", woken);
        return woken;
    }

    private async Task<bool> ReadyAsync(TicketRequirementEdge edge, CancellationToken ct)
    {
        if (!(await requirements.RequiredOfAsync(edge.Tracker, edge.TicketKey, ct)).All(e => e.StartsWhenMet)) return false;
        if ((await closed.OfAsync(edge.Tracker, [edge.TicketKey], ct)).Count > 0) return false;
        if ((await unmet.OfAsync(edge.Tracker, edge.TicketKey, ct)).Unmet.Count > 0) return false;
        return await unmet.FiledCompletelyAsync(edge.Tracker, edge.TicketKey, ct)
            && await attempts.LatestAsync(edge.Project, edge.TicketId, null, ct) is null // 2026-11-19-1b72a: a dropped start is none
            && !await attempts.HasQueuedAsync(edge.Project, edge.TicketId, ct)
            && await leases.GetByTicketAsync(edge.Project, new TicketId(edge.TicketId), ct) is null;
    }

    private Task NudgeAsync(TicketRequirementEdge edge, CancellationToken ct) =>
        nudges.EnqueueAsync(new ReworkNudgeRequest(edge.Project, edge.TicketId, ReworkNudgeOrigin.RequirementsMet), ct);
}
