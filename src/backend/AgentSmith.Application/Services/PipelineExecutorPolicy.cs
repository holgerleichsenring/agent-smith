using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services;

/// <summary>
/// Pure helpers consumed by <see cref="PipelineExecutor"/> — concurrency
/// resolution and the &quot;pipeline parked / skipped&quot; context inspection
/// that signals clean halts (Plan emitted open questions, or zero steps).
/// </summary>
internal static class PipelineExecutorPolicy
{
    public static int ResolveMaxConcurrent(ResolvedProject projectConfig, PipelineContext context) =>
        (context.TryGet<ResolvedPipelineConfig>(ContextKeys.ResolvedPipeline, out var rp) ? rp!.Agent : projectConfig.Agent)
        .Parallelism.MaxConcurrentSkillRounds;

    public static bool TryGetParkedReason(PipelineContext context, ILogger logger, out string message)
    {
        if (context.TryGet<bool>(ContextKeys.OpenQuestionsAwaitingAnswer, out var awaiting) && awaiting)
        {
            logger.LogInformation("Pipeline parked: Plan emitted open questions; waiting on operator reply");
            message = "Pipeline parked: awaiting_user_input";
            return true;
        }
        // p0327: the dialogue ask gate checkpointed the run — a clean park, not a
        // failure. ExecutePipelineUseCase maps this to the waiting_for_input status.
        if (context.TryGet<bool>(ContextKeys.WaitingForInput, out var waiting) && waiting)
        {
            logger.LogInformation("Pipeline parked: checkpointed while waiting for a dialogue answer");
            message = "Pipeline parked: waiting_for_input";
            return true;
        }
        if (context.TryGet<bool>(ContextKeys.EmptyPlanSkipped, out var emptyPlan) && emptyPlan)
        {
            logger.LogInformation("Pipeline skipped: Plan produced zero steps (empty_plan)");
            message = "Pipeline skipped: empty_plan";
            return true;
        }
        message = string.Empty;
        return false;
    }
}
