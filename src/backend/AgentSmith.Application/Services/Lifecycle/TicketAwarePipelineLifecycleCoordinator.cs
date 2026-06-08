using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Lifecycle;

/// <summary>
/// Server-side coordinator that wraps the pipeline run with ticket lifecycle
/// transitions (Enqueued→InProgress at start, InProgress→Done/Failed on dispose)
/// and a Redis heartbeat. No-ops gracefully when no TicketId is present in the
/// context — the executor can still call BeginAsync unconditionally.
/// </summary>
public sealed class TicketAwarePipelineLifecycleCoordinator(
    ITicketStatusTransitionerFactory transitionerFactory,
    ILogger<TicketAwarePipelineLifecycleCoordinator> logger) : IPipelineLifecycleCoordinator
{
    public async Task<IAsyncPipelineLifecycle> BeginAsync(
        ResolvedProject projectConfig, PipelineContext context, CancellationToken cancellationToken)
    {
        if (!context.TryGet<TicketId>(ContextKeys.TicketId, out var ticketId) || ticketId is null)
            return NoOpScope.Instance;

        try
        {
            var transitioner = transitionerFactory.Create(projectConfig.Tracker);
            var transition = await transitioner.TransitionAsync(
                ticketId, TicketLifecycleStatus.Enqueued, TicketLifecycleStatus.InProgress, cancellationToken);
            if (!transition.IsSuccess)
                logger.LogWarning("Enqueued → InProgress transition {Outcome}: {Error}",
                    transition.Outcome, transition.Error);

            // p0252: no Redis heartbeat here — run liveness is the DB ActiveRun lease
            // (attached + heartbeat-pumped by ExecutePipelineUseCase). The scope now
            // only carries the terminal lifecycle transition.
            return new TicketLifecycleScope(transitioner, ticketId, logger);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to start lifecycle tracking — continuing without it");
            return NoOpScope.Instance;
        }
    }

    private sealed class NoOpScope : IAsyncPipelineLifecycle
    {
        public static NoOpScope Instance { get; } = new();
        public void MarkFailed() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class TicketLifecycleScope(
        ITicketStatusTransitioner transitioner,
        TicketId ticketId,
        ILogger logger) : IAsyncPipelineLifecycle
    {
        private bool _failed;

        public void MarkFailed() => _failed = true;

        public async ValueTask DisposeAsync()
        {
            var target = _failed ? TicketLifecycleStatus.Failed : TicketLifecycleStatus.Done;

            // p0237: the run has ended — its outcome is authoritative. Transition
            // from the ticket's ACTUAL current lifecycle, not a hard-coded
            // InProgress. A re-run starts from a prior terminal tag (a previous
            // run may have left agent-smith:failed); with the old hard-coded
            // InProgress source the terminal write precondition-failed, so a
            // now-successful re-run left the stale failed tag in place. Reading
            // current first lets the terminal write land from whatever state the
            // ticket is in. (BeginAsync's Enqueued→InProgress stays strict — that
            // is the double-claim concurrency guard, not a run-end write.)
            TicketLifecycleStatus? current = null;
            try { current = await transitioner.ReadCurrentAsync(ticketId, CancellationToken.None); }
            catch (Exception ex) { logger.LogWarning(ex, "Failed to read current lifecycle before terminal transition"); }
            var from = current ?? TicketLifecycleStatus.Pending;

            var result = await transitioner.TransitionAsync(
                ticketId, from, target, CancellationToken.None);
            if (!result.IsSuccess)
                logger.LogWarning("{From} → {Target} transition {Outcome}: {Error}",
                    from, target, result.Outcome, result.Error);
        }
    }
}
