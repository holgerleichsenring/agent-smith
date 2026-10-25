using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Events;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-10-13-8762o: puts a default a master took on the run's event stream as event 88, where it becomes a
/// pending row a person can ratify later (2026-10-25-9324e: memories no longer go this way). Built per master start
/// where the pipeline holds a run id; a publish that fails is logged and answered false, never thrown.
/// </summary>
public sealed class DefaultDecisionRecorder(
    IEventPublisher publisher, string runId, string? specId, string? conversation, ILogger logger)
{
    public Task<bool> RecordAsync(
        string question, string chosen, IReadOnlyList<string> alternatives, string why, bool holdsSeries,
        CancellationToken ct) =>
        PublishAsync(question, chosen, alternatives, why, holdsSeries, DefaultDecisionKinds.Decision, null, ct);

    private async Task<bool> PublishAsync(
        string question, string chosen, IReadOnlyList<string> alternatives, string why, bool holdsSeries,
        string kind, string? pointerPath, CancellationToken ct)
    {
        try
        {
            await publisher.PublishAsync(new DefaultDecisionTakenEvent(
                runId, DateTimeOffset.UtcNow, specId, question, chosen, alternatives, why, holdsSeries, kind,
                pointerPath, conversation), ct);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to publish DefaultDecisionTaken ({Kind}) for run {RunId}", kind, runId);
            return false;
        }
    }
}
