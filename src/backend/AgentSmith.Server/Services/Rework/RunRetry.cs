using AgentSmith.Application.Services.Tickets;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Rework;

/// <summary>
/// 2026-10-13-9a59n: Retry — an unchanged attempt, only after an infrastructure end. The runs decide first
/// (<see cref="RunRetryGate"/>), then the tracker, read once: a ticket a person closed is not reopened. While an
/// attempt is already starting the request is refused, naming who asked. Otherwise the request is stored as a
/// row of the run and an Operator nudge goes through the rework queue, whose worker enters through the rework
/// entry like every other act; the stored request stays a pending act until an attempt starts after it.
/// </summary>
public sealed class RunRetry(
    RunRepository runs,
    AgentSmithConfig config,
    RunRetryGate gate,
    ITicketProviderFactory trackers,
    TicketClosure closure,
    IActiveRunLease leases,
    IPreviousAttemptReader attempts,
    RunRetryRequestRepository requests,
    IReworkNudges nudges,
    TimeProvider clock,
    ILogger<RunRetry> logger)
{
    public async Task<RunRetryResult> RetryAsync(string runId, string principal, CancellationToken ct)
    {
        var run = await runs.GetRunDetailAsync(runId, ct);
        if (run is null) return RunRetryResult.UnknownRun;
        if (string.IsNullOrWhiteSpace(run.TicketId)) return RunRetryResult.Invalid("This run has no ticket to retry.");
        if (run.Pipeline != PipelinePresets.CodeName)
            return RunRetryResult.Invalid("Only an attempt at a ticket's work — a code run — is retried.");
        if (!config.Projects.TryGetValue(run.Project, out var project))
            return RunRetryResult.Invalid($"Project '{run.Project}' is not configured.");
        var ticketId = run.TicketId;
        if (await gate.CheckAsync(project.Name, ticketId, runId, ct) is { } refused) return RunRetryResult.Refused(refused);
        bool closed;
        try
        {
            var provider = trackers.Create(project.Tracker);
            closed = await closure.ClosedForWorkAsync(project, provider, await provider.GetTicketAsync(new TicketId(ticketId), ct), ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Retry of run {RunId}: ticket {Ticket} could not be read from its tracker", runId, ticketId);
            return RunRetryResult.Unavailable($"The tracker could not be read ({ex.Message}); nothing was recorded.");
        }
        if (closed)
            return RunRetryResult.Refused(new(RetryRefusals.ClosedForWork, RetryRefusals.ClosedForWorkText, project.Name, ticketId));
        if (await StartingAsync(project.Name, ticketId, runId, ct) is { } starting)
            return RunRetryResult.Refused(new(RetryRefusals.Starting, starting, project.Name, ticketId));
        await requests.UpsertAsync(runId, principal, clock.GetUtcNow(), ct);
        await nudges.EnqueueAsync(new ReworkNudgeRequest(
            project.Name, ticketId, ReworkNudgeOrigin.Operator, Channel: ReworkChannel.Retry), ct);
        logger.LogInformation("Retry of run {RunId} of {Project}/#{Ticket} requested by {Principal}",
            runId, project.Name, ticketId, principal);
        return RunRetryResult.Requested(ticketId);
    }

    // A live lease or a queued code run: an attempt is already on its way, and a second request would only race it.
    private async Task<string?> StartingAsync(string project, string ticketId, string runId, CancellationToken ct)
    {
        if (await leases.GetByTicketAsync(project, new TicketId(ticketId), ct) is null
            && !await attempts.HasQueuedAsync(project, ticketId, ct)) return null;
        return await requests.ForRunAsync(runId, ct) is { } first
            ? $"an attempt is already starting — {first.RequestedBy} retried this run at {first.RequestedAt:u}"
            : "an attempt is already starting on this ticket";
    }
}
