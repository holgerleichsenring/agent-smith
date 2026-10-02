using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Lifecycle;

/// <summary>
/// 2026-10-02-5ab2b: what a popped request must pass before it runs, extracted from
/// PipelineQueueConsumer. At the POP — before the semaphore wait — the run row's claim: one
/// copy of a stored request wins and its row is beaten from then on; a copy that lost, or one
/// whose run already runs, is dropped. After the wait, the p0330 pre-start cancel gate.
/// </summary>
public sealed class RunStartGate(
    IRunStartClaim claim,
    IRunCancelStateReader cancelState,
    RunHeartbeatPump pump,
    ILogger<RunStartGate> logger)
{
    /// <summary>The start this consumer may make, or null when this copy is dropped.</summary>
    public async Task<ClaimedRunStart?> ClaimAsync(PipelineRequest request, CancellationToken ct)
    {
        if (request.RunId is not { Length: > 0 } runId) return new ClaimedRunStart(null, null);
        var outcome = await TryClaimAsync(runId, ct);
        if (outcome is null or RunStartClaimOutcome.Duplicate) return null;
        if (outcome == RunStartClaimOutcome.Unclaimed) return new ClaimedRunStart(null, null);

        var beat = new CancellationTokenSource();
        return new ClaimedRunStart(beat, pump.RunAsync(request.ProjectName, ticketId: null, runId, beat.Token));
    }

    /// <summary>
    /// p0330: the operator may have cancelled while the request sat in the queue or on the
    /// semaphore — the persisted row is the authority; flagged or finished (5f89d) never starts.
    /// A refused run is finished 'cancelled' here and its ticket handed back.
    /// </summary>
    public async Task<bool> RefusesStartAsync(IServiceProvider scoped, PipelineRequest request, CancellationToken ct)
    {
        if (request.RunId is not { Length: > 0 } runId || !await cancelState.IsStartRefusedAsync(runId, ct))
            return false;
        await ShortCircuitCancelledAsync(scoped, request, runId);
        return true;
    }

    private async Task<RunStartClaimOutcome?> TryClaimAsync(string runId, CancellationToken ct)
    {
        try
        {
            var outcome = await claim.ClaimAsync(runId, ct);
            if (outcome == RunStartClaimOutcome.Duplicate)
                logger.LogInformation("Dropped a copy of run {RunId}: another consumer claimed it, or it already runs", runId);
            return outcome;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Not started: the row keeps its request, so the sweeper pushes it again.
            logger.LogWarning(ex, "Could not claim run {RunId} — the copy is dropped and recovered from its row", runId);
            return null;
        }
    }

    // p0330: finish the reserved row 'cancelled' via the terminal event (CancellationToken.None:
    // it must land even mid-shutdown) and hand the ticket back. p0459: runId null — this runs
    // before the run attaches, so only the claim's unattached row goes.
    private async Task ShortCircuitCancelledAsync(IServiceProvider scoped, PipelineRequest request, string runId)
    {
        logger.LogInformation(
            "Run {RunId} ({Project}/#{Ticket}) was cancelled before start — short-circuiting",
            runId, request.ProjectName, request.TicketId?.Value ?? "—");
        await scoped.GetRequiredService<IEventPublisher>().PublishAsync(
            new RunFinishedEvent(runId, "cancelled", null, "cancelled before start (operator)", DateTimeOffset.UtcNow),
            CancellationToken.None);
        if (request.TicketId is not null)
            await scoped.GetRequiredService<IActiveRunLease>()
                .ReleaseAsync(request.ProjectName, request.TicketId, runId: null, CancellationToken.None);
    }
}
