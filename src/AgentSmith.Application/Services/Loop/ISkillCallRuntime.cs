using AgentSmith.Application.Models;
using AgentSmith.Application.Services;

namespace AgentSmith.Application.Services.Loop;

/// <summary>
/// Wraps M.E.AI's FunctionInvokingChatClient with hard limits, the five-state
/// outcome contract, per-skill cost tracking, and read-scope enforcement.
/// PipelineCostTracker is per-pipeline-run state passed in by the caller (matches
/// the existing GetOrCreate-from-PipelineContext convention). One implementation:
/// <see cref="SkillCallRuntime"/>.
/// </summary>
public interface ISkillCallRuntime
{
    Task<SkillCallResult> ExecuteAsync(
        SkillCallRequest request,
        PipelineCostTracker costTracker,
        CancellationToken cancellationToken);
}
