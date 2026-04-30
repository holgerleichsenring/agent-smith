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
    IJobHeartbeatService heartbeat,
    ILogger<TicketAwarePipelineLifecycleCoordinator> logger) : IPipelineLifecycleCoordinator
{
    public async Task<IAsyncPipelineLifecycle> BeginAsync(
        ProjectConfig projectConfig, PipelineContext context, CancellationToken cancellationToken)
    {
        if (!context.TryGet<TicketId>(ContextKeys.TicketId, out var ticketId) || ticketId is null)
            return NoOpScope.Instance;

        try
        {
            var transitioner = transitionerFactory.Create(projectConfig.Tickets);
            var transition = await transitioner.TransitionAsync(
                ticketId, TicketLifecycleStatus.Enqueued, TicketLifecycleStatus.InProgress, cancellationToken);
            if (!transition.IsSuccess)
                logger.LogWarning("Enqueued → InProgress transition {Outcome}: {Error}",
                    transition.Outcome, transition.Error);

            return new TicketLifecycleScope(transitioner, heartbeat.Start(ticketId), ticketId, logger);
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
        IAsyncDisposable heartbeat,
        TicketId ticketId,
        ILogger logger) : IAsyncPipelineLifecycle
    {
        private bool _failed;

        public void MarkFailed() => _failed = true;

        public async ValueTask DisposeAsync()
        {
            await heartbeat.DisposeAsync();
            var target = _failed ? TicketLifecycleStatus.Failed : TicketLifecycleStatus.Done;
            var result = await transitioner.TransitionAsync(
                ticketId, TicketLifecycleStatus.InProgress, target, CancellationToken.None);
            if (!result.IsSuccess)
                logger.LogWarning("InProgress → {Target} transition {Outcome}: {Error}",
                    target, result.Outcome, result.Error);
        }
    }
}
