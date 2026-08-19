using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0466: the one writer of a phase's standing. Updates the pipeline's per-phase table
/// (which the pull request renders) and publishes the same fact as an event, because the
/// event stream is the only DB channel a spawned orchestrator has.
/// </summary>
public sealed class PhaseProgressRecorder(IEventPublisher eventPublisher) : IPhaseProgressRecorder
{
    public async Task RecordAsync(
        PipelineContext pipeline,
        string phaseId,
        PhaseRunState state,
        string? failingCommand = null,
        string? note = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (!pipeline.TryGet<SpecSet>(ContextKeys.SpecSet, out var set) || set is null) return;

        var progress = pipeline.TryGet<SpecSequenceProgress>(
            ContextKeys.SpecSequenceProgress, out var p) && p is not null
            ? p : SpecSequenceProgress.ForSet(set);
        pipeline.Set(
            ContextKeys.SpecSequenceProgress, progress.With(phaseId, state, failingCommand, note));

        await PublishAsync(pipeline, set, phaseId, state, failingCommand ?? note, cancellationToken);
    }

    // The ordinal and the title come from the SET, which is the only place that knows
    // where a phase sits in the sequence and what it was asked to do.
    private Task PublishAsync(
        PipelineContext pipeline, SpecSet set, string phaseId, PhaseRunState state,
        string? verdict, CancellationToken ct)
    {
        if (!pipeline.TryGet<string>(ContextKeys.RunId, out var runId) || string.IsNullOrEmpty(runId))
            return Task.CompletedTask;
        var ordinal = set.Phases.ToList().FindIndex(x => x.PhaseId == phaseId) + 1;
        var title = set.Phases.FirstOrDefault(x => x.PhaseId == phaseId)?.Draft.Goal ?? phaseId;
        return eventPublisher.PublishAsync(
            new PhaseStateChangedEvent(
                runId, phaseId, ordinal, title, state, verdict, DateTimeOffset.UtcNow), ct);
    }
}
