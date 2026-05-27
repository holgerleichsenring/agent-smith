using AgentSmith.Application.Services;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.SkillRounds;

/// <summary>
/// p0147d: Owns the SkillRoundBuffer → PipelineContext merge previously held as
/// static methods on SkillRoundHandlerBase. Both the round handlers (via
/// <see cref="Dispatch"/>) and PipelineBatchRunner (via
/// <see cref="ApplyBufferToContext"/>) call through the same instance.
/// </summary>
public sealed class SkillRoundBufferDispatcher : ISkillRoundBufferDispatcher
{
    public void Dispatch(PipelineContext pipeline, SkillRoundBuffer buffer)
    {
        if (pipeline.TryGet<List<SkillRoundBuffer>>(
                ContextKeys.DeferredBuffers, out var deferred) && deferred is not null)
        {
            lock (deferred) deferred.Add(buffer);
            return;
        }
        ApplyBufferToContext(pipeline, buffer);
    }

    public void ApplyBufferToContext(PipelineContext pipeline, SkillRoundBuffer buffer)
    {
        if (buffer.Observations.Count > 0) ApplyObservations(pipeline, buffer.Observations);
        if (buffer.DiscussionEntry is not null) ApplyDiscussionEntry(pipeline, buffer.DiscussionEntry);
        if (buffer.StructuredOutput is not null)
            ApplyStructuredOutput(pipeline, buffer.SkillName, buffer.StructuredOutput);
    }

    private static void ApplyObservations(PipelineContext pipeline, IReadOnlyList<SkillObservation> parsed)
    {
        if (!pipeline.TryGet<List<SkillObservation>>(
                ContextKeys.SkillObservations, out var observations) || observations is null)
            observations = [];
        var nextId = observations.Count > 0 ? observations.Max(o => o.Id) + 1 : 1;
        foreach (var obs in parsed) observations.Add(obs with { Id = nextId++ });
        pipeline.Set(ContextKeys.SkillObservations, observations);
    }

    private static void ApplyDiscussionEntry(PipelineContext pipeline, DiscussionEntry entry)
    {
        if (!pipeline.TryGet<List<DiscussionEntry>>(
                ContextKeys.DiscussionLog, out var log) || log is null)
            log = [];
        log.Add(entry);
        pipeline.Set(ContextKeys.DiscussionLog, log);
    }

    private static void ApplyStructuredOutput(PipelineContext pipeline, string skillName, string output)
    {
        if (!pipeline.TryGet<Dictionary<string, string>>(
                ContextKeys.SkillOutputs, out var outputs) || outputs is null)
            outputs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        outputs[skillName] = output;
        pipeline.Set(ContextKeys.SkillOutputs, outputs);
    }
}
