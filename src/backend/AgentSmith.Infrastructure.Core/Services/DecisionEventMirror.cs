using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Events;

namespace AgentSmith.Infrastructure.Core.Services;

/// <summary>
/// Shared decision → event mapping for both <see cref="InMemoryDecisionLogger"/>
/// and <see cref="FileDecisionLogger"/>. Today's IDecisionLogger signature is
/// single-string ("chose X over Y because Z"); we mirror that into Chose with
/// Over=null and Reason=sourceLabel for now. A follow-up phase can split the
/// signature into structured fields without re-shaping the event contract.
/// </summary>
internal static class DecisionEventMirror
{
    public static async Task PublishAsync(
        IEventPublisher eventPublisher,
        IRunContextAccessor runContext,
        DecisionCategory category,
        string decision,
        string? sourceLabel,
        CancellationToken cancellationToken)
    {
        var runId = runContext.CurrentRunId;
        if (string.IsNullOrEmpty(runId)) return;
        await eventPublisher.PublishAsync(
            new DecisionLoggedEvent(
                runId,
                category.ToString(),
                decision,
                Over: null,
                Reason: sourceLabel ?? string.Empty,
                DateTimeOffset.UtcNow),
            cancellationToken);
    }
}
