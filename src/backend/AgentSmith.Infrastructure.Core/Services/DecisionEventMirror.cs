using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Events;

namespace AgentSmith.Infrastructure.Core.Services;

/// <summary>
/// The decision → event mapping <see cref="RepositoryDecisionLogger"/> publishes through — the
/// run's own record of a decision, of which the repository file is a copy. Today's signature is
/// single-string ("chose X over Y because Z"); we mirror that into Chose with
/// Over=null and Reason=sourceLabel for now. A follow-up phase can split the
/// signature into structured fields without re-shaping the event contract.
/// </summary>
public sealed class DecisionEventMirror(
    IEventPublisher eventPublisher,
    IRunContextAccessor runContext)
{
    public async Task PublishAsync(
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
                DateTimeOffset.UtcNow,
                // p0466: the phase the decision was taken in, from the ambient step
                // frame. The alternative — attributing decisions to a phase by the
                // step index they share — would be the parser again, one join down.
                PhaseId: runContext.CurrentPhaseId),
            cancellationToken);
    }
}
