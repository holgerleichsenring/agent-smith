using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Pocs.Rollout;

/// <summary>2026-11-23-c81ec: shows one Argo reading as a progress line of the running step (an L1StepDetail event),
/// which the run page and the Rollout page read live. A line that cannot be shown never stops the follow.</summary>
public sealed class PocRolloutLines(IEventPublisher events, IRunContextAccessor runContext, ILogger<PocRolloutLines> logger)
{
    public const string Origin = "argo-rollout";

    public async Task ShowAsync(PipelineContext pipeline, string line, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        logger.LogInformation("Rollout: {Line}", line);
        if (!pipeline.TryGet<string>(ContextKeys.RunId, out var runId) || string.IsNullOrEmpty(runId)) return;
        try
        {
            await events.PublishAsync(new L1StepDetailEvent(runId, runContext.CurrentStepIndex ?? 0, Origin, line, DateTimeOffset.UtcNow), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "Run {RunId}: the rollout line could not be shown", runId);
        }
    }
}
