using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Extensions;

/// <summary>
/// Extension methods for PipelineContext to avoid duplication across handlers.
/// </summary>
public static class PipelineContextExtensions
{
    public static void AppendDecisions(this PipelineContext pipeline, IReadOnlyList<PlanDecision> decisions)
    {
        pipeline.TryGet<List<PlanDecision>>(ContextKeys.Decisions, out var existing);
        var all = existing ?? new List<PlanDecision>();
        all.AddRange(decisions);
        pipeline.Set(ContextKeys.Decisions, all);
    }
}
