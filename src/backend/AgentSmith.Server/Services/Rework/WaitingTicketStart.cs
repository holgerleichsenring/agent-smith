using AgentSmith.Application.Services.Claim;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Series;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Runs;
using AgentSmith.Server.Services.ChatLaunch;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Rework;

/// <summary>
/// 2026-11-19-1b72a: the one start of a ticket that waited — lifted out of RequirementsMetStart (removed 2026-11-25-96aea) so a
/// series' resume starts exactly as the wake does. Never one a person marked done by hand or closed, one the tracker
/// counts closed, one a paused series holds, one whose requirements are unmet (read as every start reads them, so
/// on_green_push counts), whose filing did not finish or whose edge waits for a start by name.
/// Never a second start either: a ticket with an attempt, a queued code run or a live lease is under way already
/// (<see cref="UnderWayAsync"/>, as DependentsWake reads it) — under a nudge's ticket lock that dedupes two wakes.
/// <para>2026-11-26-5f6e: answered as what it came to — the claim's run when the poller's route claimed one, the queue's reason
/// when it queued, and otherwise refused with why, each hold naming what lifts it. A start by name (start_ticket) is the
/// start a by-name edge waits for, so only a resume leaves such a ticket to it.</para>
/// <para>2026-11-27-3532a: a started ticket goes straight through the run-start path (<see cref="ChatTicketRunLauncher"/>, marked
/// as the series' start): the requirement gate, the FIFO capacity queue, the reservation and the claim with its lease. No 'start'
/// run, no tag, no label route; a queued start names its place in the queue.</para>
/// </summary>
public sealed class WaitingTicketStart(
    ITicketRequirementStore requirements,
    TicketStartHold hold,
    UnmetTicketRequirements unmet,
    IServiceScopeFactory scopes,
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
        ChatLaunchResult launch;
        await using (var scope = scopes.CreateAsyncScope()) // 2026-11-27-3532a: the run-start path, by name, marked as the series'
            launch = await scope.ServiceProvider.GetRequiredService<ChatTicketRunLauncher>()
                .LaunchAsync(project.Name, ticketId, PipelinePresets.CodeName, ct, ReworkStartKind.Series);
        var lease = launch.TakenByAnother ? await leases.GetByTicketAsync(project.Name, new TicketId(ticketId), ct) : null;
        var place = launch is { WaitReason: not null, RunId: { } queued }
            ? (await queue.GetPositionsByRunIdAsync(ct)).GetValueOrDefault(queued) : 0;
        var outcome = SpawnedStart.Of(ticketId, launch, place, lease?.RunId);
        if (!outcome.Started) logger.LogInformation("Ticket {Project}/{Ticket} was not started now: {Outcome}", project.Name, ticketId, outcome.Text);
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
