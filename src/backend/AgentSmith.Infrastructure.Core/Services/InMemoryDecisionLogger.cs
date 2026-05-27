using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Events;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Core.Services;

/// <summary>
/// No-op decision logger for pipelines without a target repository.
/// Decisions still flow through PipelineContext to result.md — this logger
/// skips the file-based decisions.md write but mirrors the entry into the
/// per-run event stream when a run scope is active (p0169e).
/// </summary>
public sealed class InMemoryDecisionLogger(
    IEventPublisher eventPublisher,
    IRunContextAccessor runContext,
    ILogger<InMemoryDecisionLogger> logger) : IDecisionLogger
{
    public async Task LogAsync(string? repoPath, DecisionCategory category,
                               string decision, CancellationToken cancellationToken = default,
                               string? sourceLabel = null)
    {
        logger.LogDebug("Decision logged in-memory [{Source}/{Category}]: {Decision}",
            sourceLabel ?? "global", category, decision);
        await DecisionEventMirror.PublishAsync(
            eventPublisher, runContext, category, decision, sourceLabel, cancellationToken);
    }
}
