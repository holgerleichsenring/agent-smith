using AgentSmith.Application.Services.Claim;
using AgentSmith.Application.Services.Filing;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Series;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Services.Acts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Rework;

/// <summary>
/// 2026-11-19-1b72a: the one start of a ticket that waited — lifted out of RequirementsMetStart (removed 2026-11-25-96aea) so a
/// series' resume starts exactly as the wake does. Never one a person marked done by hand or closed, one the tracker
/// counts closed, one a paused series holds, one whose requirements are unmet (read as every start reads them, so
/// on_green_push counts), whose filing did not finish or whose edge waits for a start by name. A started ticket is
/// tagged or moved by a 'start' run, then claimed through the poller's own route, since in webhook mode nothing else
/// delivers a ticket that already sits in a trigger status. A start run that ended first throws, so a nudge reschedules.
/// Never a second start either: a ticket with an attempt, a queued code run or a live lease is under way already
/// (<see cref="UnderWayAsync"/>, as DependentsWake reads it) — under a nudge's ticket lock that dedupes two wakes.
/// <para>2026-11-26-5f6e: answered as what it came to — the claim's run when the poller's route claimed one, the queue's reason
/// when it queued, and otherwise refused with why, each hold naming what lifts it. A start by name (start_ticket) is the
/// start a by-name edge waits for, so only a resume leaves such a ticket to it.</para>
/// </summary>
public sealed class WaitingTicketStart(
    ITicketRequirementStore requirements,
    TicketStartHold hold,
    UnmetTicketRequirements unmet,
    IServiceScopeFactory scopes,
    ReworkFallbackSpawn fallback,
    IPreviousAttemptReader attempts,
    IActiveRunLease leases,
    ICapacityQueue queue,
    ILogger<WaitingTicketStart> logger)
{
    /// <summary>Where the ticket stands already — waiting for capacity, running, queued or worked — or null when nothing is.
    /// 2026-11-26-a12eb: read before anything is tagged or moved, and said as the state it is, with since when and the run.</summary>
    public async Task<string?> UnderWayAsync(string project, string ticketId, CancellationToken ct)
    {
        if (await queue.FindAsync(project, ticketId, ct) is { } queued)
            return $"it is already queued since {queued.EnqueuedAt:yyyy-MM-dd HH:mm} UTC, waiting for sandbox capacity: {queued.Reason}"
                + (queued.ReservedRunId is { Length: > 0 } run ? $" (run {run})" : string.Empty);
        if (await leases.GetByTicketAsync(project, new TicketId(ticketId), ct) is { } lease)
            return $"it is already running (run {lease.RunId ?? "not recorded yet"})";
        if (await attempts.HasQueuedAsync(project, ticketId, ct)) return "a run of it is already queued";
        return await attempts.HasAttemptAsync(project, ticketId, ct) ? "it was worked already; retry_ticket runs it again" : null;
    }

    /// <param name="byName">True for start_ticket, the start a by-name edge waits for; false for a resume.</param>
    public async Task<SeriesStart> StartAsync(ResolvedProject project, string ticketId, bool byName, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (await HeldAsync(project, ticketId, byName, ct) is { } held)
            return SeriesStart.Refused($"Ticket {ticketId} was not started: {held}.");
        if (!await hold.OpenAsync(project, ticketId, ct))
            return SeriesStart.Refused($"Ticket {ticketId} was not started: it is closed, or the tracker could not be read; it starts once "
                + "the tracker reads it open again.");
        (FiledWorkStart? Start, string? Ended) run;
        var fromText = await unmet.FiledFromTextAsync(project.Name, ticketId, ct); // 5f6e: it gets its trigger label
        await using (var scope = scopes.CreateAsyncScope())
            run = await scope.ServiceProvider.GetRequiredService<TicketStartRun>().StartAsync(project, ticketId, mayStartRuns: true, fromText);
        var started = run.Start ?? throw new InvalidOperationException(
            $"Ticket {project.Name}/{ticketId}'s start run ended before it started anything: {run.Ended}");
        if (started.State != FiledStartState.Started)
        {
            logger.LogWarning("Ticket {Project}/{Ticket} was not started: {Reason}", project.Name, ticketId, started.Reason);
            return SeriesStart.Refused($"Ticket {ticketId} was not started: {started.Reason}");
        }
        var (spawned, nowhere) = await fallback.SpawnAsync(project, ticketId, ct);
        var outcome = SpawnedStart.Of(ticketId, started.Reason, spawned, nowhere);
        // Claimable now, so a webhook or the poller may have claimed it first: the run that holds it is this start's run.
        if (!outcome.Started && await leases.GetByTicketAsync(project.Name, new TicketId(ticketId), ct) is { } lease)
            outcome = new(SeriesStartKind.Started, $"Ticket {ticketId} started: run {lease.RunId ?? "(its id is not recorded yet)"}.", lease.RunId);
        if (!outcome.Started) logger.LogWarning("Ticket {Project}/{Ticket} was made claimable but no run started: {Outcome}",
            project.Name, ticketId, outcome.Text);
        return outcome;
    }

    // What keeps the ticket from its start, or null, with what lifts it. 2026-11-26-5f6e: the wake's only-with-requirements
    // hold and its pending flag had no caller since 2026-11-25-96aea and are gone.
    private async Task<string?> HeldAsync(ResolvedProject project, string ticketId, bool byName, CancellationToken ct)
    {
        var tracker = project.Tracker.Name;
        var key = TicketKey.For(project.Tracker.Type.ToString().ToLowerInvariant(), ticketId).Value;
        var edges = await requirements.RequiredOfAsync(tracker, key, ct);
        if (await UnderWayAsync(project.Name, ticketId, ct) is { } underWay) return $"{underWay}; nothing was done";
        if (await hold.MarkedAsync(project, ticketId, ct)) return "it was marked done by hand or closed; reopen it to start it";
        if (await unmet.PausedSeriesAsync(project.Name, ticketId, ct) is { } paused) return $"series {paused.Id} is paused; resume it first";
        if (edges.Count == 0) return null;
        if ((await unmet.ForStartAsync(project, key, ct)).Unmet is { Count: > 0 } open) return RequiresUnmetGate.Sentence(open).TrimEnd('.');
        if (!await unmet.FiledCompletelyAsync(tracker, key, project.Name, ticketId, ct)) return Incomplete(project.Name, ticketId);
        if (byName || edges.All(e => e.StartsWhenMet)) return null;
        logger.LogInformation("Ticket {Project}/{Ticket}'s requirements landed; it waits for a start by name", project.Name, ticketId);
        return "it waits for a start by name; start_ticket starts it";
    }

    private string Incomplete(string project, string ticketId)
    {
        logger.LogWarning("Ticket {Project}/{Ticket}'s filing is incomplete; not started", project, ticketId);
        return "its filing did not finish (no approved set was stored for it); close it and approve the list again";
    }
}
