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

            // p0262: the run-end tag is a pure marker, written UNCONDITIONALLY by the
            // platform transitioner — no need to read the current lifecycle to anchor
            // `from` (that p0237 read existed only to satisfy the now-removed precondition).
            // Pass the expected prior state (InProgress) advisorily; the write lands
            // regardless of the tag's actual current value.
            var result = await transitioner.TransitionAsync(
                ticketId, TicketLifecycleStatus.InProgress, target, CancellationToken.None);
            if (!result.IsSuccess)
                logger.LogWarning("InProgress → {Target} transition {Outcome}: {Error}",
                    target, result.Outcome, result.Error);
        }
    }
}
