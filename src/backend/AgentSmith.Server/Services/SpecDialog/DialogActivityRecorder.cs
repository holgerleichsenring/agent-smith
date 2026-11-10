using AgentSmith.Contracts.Events;
using AgentSmith.Server.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-11-10-d854c: keeps each step a design turn reports as a run event on the turn's own run, so the steps outlive
/// the turn's memory and a server restart. A step reported before the run has an id is not kept. It never throws:
/// a step is reported from inside work that publishes its own outcome afterwards.
/// </summary>
public sealed class DialogActivityRecorder(
    IEventPublisher publisher, IRunContextAccessor runs, ILogger<DialogActivityRecorder> logger)
{
    public async Task RecordAsync(SpecDialogActivityPush step, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(step);
        if (runs.CurrentRunId is not { Length: > 0 } runId) return;
        try
        {
            await publisher.PublishAsync(new DialogActivityEvent(
                runId, step.At, step.Kind, step.Name, Bounded(step.Detail), step.Seq, step.TurnStartedAt), ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not keep step {Seq} of run {RunId}", step.Seq, runId);
        }
    }

    private static string? Bounded(string? detail) =>
        detail is { Length: > DialogActivityEvent.MaxDetail } ? detail[..DialogActivityEvent.MaxDetail] : detail;
}
