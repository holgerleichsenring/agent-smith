using AgentSmith.Application.Services.Claim;
using AgentSmith.Application.Services.Filing;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Services.Acts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Rework;

/// <summary>
/// 2026-11-19-1b72a: the one start of a ticket that waited — lifted out of <see cref="RequirementsMetStart"/> so a
/// series' resume starts exactly as the wake does. Never one a person marked done by hand or closed, one the tracker
/// counts closed, one a paused series holds, one whose requirements are unmet (read as every start reads them, so
/// on_green_push counts), whose filing did not finish or whose edge waits for a start by name. A started ticket is
/// tagged or moved by a 'start' run, then claimed through the poller's own route, since in webhook mode nothing else
/// delivers a ticket that already sits in a trigger status. A start run that ended first throws, so a nudge reschedules.
/// Never a second start either: a ticket with an attempt, a queued code run or a live lease is under way already
/// (<see cref="UnderWayAsync"/>, as DependentsWake reads it) — under a nudge's ticket lock that dedupes two wakes.
/// </summary>
public sealed class WaitingTicketStart(
    ITicketRequirementStore requirements,
    TicketStartHold hold,
    UnmetTicketRequirements unmet,
    IServiceScopeFactory scopes,
    ReworkFallbackSpawn fallback,
    IPreviousAttemptReader attempts,
    IActiveRunLease leases,
    ILogger<WaitingTicketStart> logger)
{
    /// <summary>Why the ticket is under way already — worked, queued or held by a live run — or null when nothing is.</summary>
    public async Task<string?> UnderWayAsync(string project, string ticketId, CancellationToken ct)
    {
        if (await attempts.HasAttemptAsync(project, ticketId, ct)) return "it was worked already";
        if (await attempts.HasQueuedAsync(project, ticketId, ct)) return "a start of it is queued already";
        return await leases.GetByTicketAsync(project, new TicketId(ticketId), ct) is not null ? "a run holds it now" : null;
    }

    /// <param name="onlyWithRequirements">True for the wake, which starts only a ticket its requirements held, or one a
    /// running series (approver with runs.control) owns, which a resume handed it.</param>
    public async Task<WaitingStart> StartAsync(ResolvedProject project, string ticketId, bool onlyWithRequirements, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (await HeldAsync(project, ticketId, onlyWithRequirements, ct) is { } held) return new(false, $"Ticket {ticketId} is held: {held}.");
        if (!await hold.OpenAsync(project, ticketId, ct)) return new(false, $"Ticket {ticketId} is closed, or the tracker could not be read.");
        (FiledWorkStart? Start, string? Ended) run;
        await using (var scope = scopes.CreateAsyncScope())
            run = await scope.ServiceProvider.GetRequiredService<TicketStartRun>().StartAsync(project, ticketId, mayStartRuns: true);
        var started = run.Start ?? throw new InvalidOperationException(
            $"Ticket {project.Name}/{ticketId}'s start run ended before it started anything: {run.Ended}");
        if (started.State != FiledStartState.Started)
        {
            logger.LogWarning("Ticket {Project}/{Ticket} was not started: {Reason}", project.Name, ticketId, started.Reason);
            return new(false, $"Ticket {ticketId} was not started: {started.Reason}");
        }
        await fallback.SpawnAsync(project, ticketId, ct);
        return new(true, $"Ticket {ticketId} started: {started.Reason}");
    }

    // What keeps the ticket from its start, or null. An unmet requirement is silent for the wake: its landing wakes it again.
    private async Task<string?> HeldAsync(ResolvedProject project, string ticketId, bool onlyWithRequirements, CancellationToken ct)
    {
        var tracker = project.Tracker.Name;
        var key = TicketKey.For(project.Tracker.Type.ToString().ToLowerInvariant(), ticketId).Value;
        var edges = await requirements.RequiredOfAsync(tracker, key, ct);
        // A ticket without requirements is no wake's to start — unless a running series whose approver may start runs owns
        // it (a resume handed it to the nudge when its lock was busy), read when the nudge is served.
        if (edges.Count == 0 && onlyWithRequirements && await unmet.RunningSeriesAsync(project.Name, ticketId, ct) is not { MayStartRuns: true })
            return "it requires nothing, so no landing starts it";
        if (await UnderWayAsync(project.Name, ticketId, ct) is { } underWay) return underWay;
        if (await hold.MarkedAsync(project, ticketId, ct)) return "it was marked done by hand or closed";
        if (await unmet.PausedSeriesAsync(project.Name, ticketId, ct) is { } paused) return $"series {paused.Id} is paused";
        if (edges.Count == 0) return null;
        if ((await unmet.ForStartAsync(project, key, ct)).Unmet is { Count: > 0 } open) return RequiresUnmetGate.Sentence(open).TrimEnd('.');
        if (!await unmet.FiledCompletelyAsync(tracker, key, ct))
        {
            logger.LogWarning("Ticket {Project}/{Ticket}'s filing is incomplete; not started", project.Name, ticketId);
            return "its filing did not finish";
        }
        if (edges.All(e => e.StartsWhenMet)) return null;
        logger.LogInformation("Ticket {Project}/{Ticket}'s requirements landed; it waits for a start by name", project.Name, ticketId);
        return "it waits for a start by name";
    }
}
