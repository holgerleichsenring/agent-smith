using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0466: the one writer of a spec's standing. Updates the pipeline's per-spec table
/// (which the pull request renders) and publishes the same fact as an event, because the
/// event stream is the only DB channel a spawned orchestrator has.
/// </summary>
public sealed class SpecProgressRecorder(IEventPublisher eventPublisher) : ISpecProgressRecorder
{
    public async Task RecordAsync(
        PipelineContext pipeline,
        string specId,
        SpecRunState state,
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
            ContextKeys.SpecSequenceProgress, progress.With(specId, state, failingCommand, note));

        await PublishAsync(pipeline, set, specId, state, failingCommand ?? note, cancellationToken);
    }

    // The ordinal and the title come from the SET, which is the only place that knows
    // where a spec sits in the sequence and what it was asked to do.
    private Task PublishAsync(
        PipelineContext pipeline, SpecSet set, string specId, SpecRunState state,
        string? verdict, CancellationToken ct)
    {
        if (!pipeline.TryGet<string>(ContextKeys.RunId, out var runId) || string.IsNullOrEmpty(runId))
            return Task.CompletedTask;
        var ordinal = set.Specs.ToList().FindIndex(x => x.SpecId == specId) + 1;
        var title = set.Specs.FirstOrDefault(x => x.SpecId == specId)?.Draft.Goal ?? specId;
        return eventPublisher.PublishAsync(
            new SpecStateChangedEvent(
                runId, specId, ordinal, title, state, verdict, DateTimeOffset.UtcNow), ct);
    }
}
