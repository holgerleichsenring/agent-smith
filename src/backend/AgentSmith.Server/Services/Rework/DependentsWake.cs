using AgentSmith.Application.Services.Claim;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Models;
using AgentSmith.Contracts.Series;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Rework;

/// <summary>
/// 2026-10-14-dbceh: a landing wakes the tickets that wait for it. 2026-11-25-96aea: it starts none — it records
/// "&lt;prerequisite&gt; landed" on the series that holds each dependent (<see cref="ISeriesWakes"/>), once per landing and
/// dependent, and that series' master decides whether to start, retarget or merge it. Called by
/// <see cref="TicketLandingRecorder"/> after a WON insert only, so one replica records it, and by a series' delivery.
/// <para>A wake lost to a crash is recovered, not trusted: the periodic re-read asks <see cref="RecoverAsync"/>, which
/// re-derives it from the requirement and landing rows; the once-key keeps a recovered wake from waking twice.</para>
/// </summary>
public sealed class DependentsWake(
    ITicketRequirementStore requirements,
    ITicketLandingStore landings,
    ITicketClosedMarkStore closed, // 2026-10-15-114fa: a closed dependent is not woken
    UnmetTicketRequirements unmet,
    IPreviousAttemptReader attempts,
    IActiveRunLease leases,
    ISeriesWakes wakes,
    TimeProvider clock,
    ILogger<DependentsWake> logger)
{
    /// <summary>Records the landing on the series of every distinct dependent; answers how many. A failure is logged — the
    /// recovery retries it. <paramref name="occasion"/> keys a series' delivery apart from the landing on its branch.</summary>
    public async Task<int> WakeAsync(TicketLandingFact landed, CancellationToken ct, string occasion = Landed)
    {
        ArgumentNullException.ThrowIfNull(landed);
        try
        {
            var dependents = (await requirements.DependentsOfAsync(landed.Tracker, landed.TicketKey, ct))
                .DistinctBy(e => e.TicketKey, StringComparer.Ordinal).ToList();
            foreach (var edge in dependents) await NudgeAsync(edge, occasion, ct);
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
            await NudgeAsync(edge, Landed, ct);
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
        return await unmet.FiledCompletelyAsync(edge.Tracker, edge.TicketKey, edge.Project, edge.TicketId, ct)
            && await attempts.LatestAsync(edge.Project, edge.TicketId, null, ct) is null // 2026-11-19-1b72a: a dropped start is none
            && !await attempts.HasQueuedAsync(edge.Project, edge.TicketId, ct)
            && await leases.GetByTicketAsync(edge.Project, new TicketId(edge.TicketId), ct) is null;
    }

    public const string Landed = "landed";

    private async Task NudgeAsync(TicketRequirementEdge edge, string occasion, CancellationToken ct)
    {
        var named = RequiresUnmetGate.Named(edge.RequiredTicketId);
        var wake = new SeriesWake(SeriesWakeKinds.Prerequisite, $"{named}, which it requires, {occasion}; start_ticket starts it once "
            + "all it requires is met") { Once = $"{occasion}:{edge.RequiredTicketId}:{edge.TicketId}" };
        if (!await wakes.WakeAsync(edge.Project, edge.TicketId, wake, ct))
            logger.LogInformation("{Project}/{Ticket}, which waited for {Required}, is held by no series; nothing was woken",
                edge.Project, edge.TicketId, named);
    }
}
