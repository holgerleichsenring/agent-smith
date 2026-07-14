using System.Text.Json;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Expectations;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Expectations;

/// <summary>
/// p0328: publishes the ratification outcome as ExpectationRatifiedEvent —
/// an EVENT, not a DB write, because the producer may be a spawned
/// orchestrator whose only DB channel is the event stream (p0330 lesson).
/// The server-side applier persists the RunExpectation row.
/// </summary>
public sealed class ExpectationOutcomeRecorder(
    IEventPublisher events,
    ILogger<ExpectationOutcomeRecorder> logger)
{
    public async Task RecordAsync(
        PipelineContext pipeline, ExpectationDraft draft, RatifiedExpectation ratified,
        CancellationToken cancellationToken)
    {
        if (!pipeline.TryGet<string>(ContextKeys.RunId, out var runId) || string.IsNullOrEmpty(runId))
        {
            logger.LogDebug("No run id in context — ratification outcome not persisted");
            return;
        }
        await events.PublishAsync(new ExpectationRatifiedEvent(
            runId!,
            DraftJson: JsonSerializer.Serialize(draft),
            RatifiedJson: JsonSerializer.Serialize(ratified.Draft),
            Outcome: ratified.Outcome,
            RatifiedBy: ratified.RatifiedBy,
            EditDistance: ratified.EditDistance,
            Timestamp: DateTimeOffset.UtcNow), cancellationToken);
        logger.LogInformation(
            "Expectation outcome recorded for run {RunId}: {Outcome} (edit distance {Distance})",
            runId, ratified.Outcome, ratified.EditDistance);
    }
}
