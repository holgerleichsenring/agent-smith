using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.Services.SkillRounds;

/// <summary>
/// p0147d: Owns the SkillRoundBuffer → PipelineContext merge that was previously
/// a pair of static methods on SkillRoundHandlerBase. PipelineBatchRunner uses
/// the same merge step in deterministic skill-graph order so the static-host
/// pattern is gone — both call sites now share an injected service.
/// </summary>
public interface ISkillRoundBufferDispatcher
{
    /// <summary>
    /// Either defers (when a DeferredBuffers list is present in the pipeline) or
    /// applies the buffer to the pipeline immediately.
    /// </summary>
    void Dispatch(PipelineContext pipeline, SkillRoundBuffer buffer);

    /// <summary>
    /// Unconditionally merges a buffer into the pipeline context — used by the
    /// executor's deferred-merge step in graph order.
    /// </summary>
    void ApplyBufferToContext(PipelineContext pipeline, SkillRoundBuffer buffer);
}
