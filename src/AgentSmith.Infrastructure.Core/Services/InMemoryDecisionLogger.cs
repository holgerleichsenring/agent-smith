using AgentSmith.Contracts.Decisions;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Core.Services;

/// <summary>
/// No-op decision logger for pipelines without a target repository.
/// Decisions still flow through PipelineContext to result.md — this logger
/// simply skips the file-based decisions.md write.
/// </summary>
public sealed class InMemoryDecisionLogger(ILogger<InMemoryDecisionLogger> logger) : IDecisionLogger
{
    public Task LogAsync(string? repoPath, DecisionCategory category,
                         string decision, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Decision logged in-memory [{Category}]: {Decision}", category, decision);
        return Task.CompletedTask;
    }
}
