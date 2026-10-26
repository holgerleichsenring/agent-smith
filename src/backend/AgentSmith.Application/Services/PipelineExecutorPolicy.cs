using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services;

/// <summary>
/// Pure helpers consumed by <see cref="PipelineExecutor"/> — the &quot;pipeline
/// parked / skipped&quot; context inspection that signals clean halts (Plan emitted
/// open questions, or zero steps).
///
/// p0312d removed ResolveMaxConcurrent together with the batch path it fed.
/// </summary>
public sealed class PipelineExecutorPolicy(ILogger<PipelineExecutorPolicy> logger)
{
    public bool TryGetParkedReason(PipelineContext context, out string message)
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
        // 2026-10-26-bdd0c: a spec derivation that ended — on its specs or on a question — stops as a success;
        // the sentence is the run's summary.
        if (context.TryGet<string>(ContextKeys.SpecDerivationEnded, out var ended) && !string.IsNullOrEmpty(ended))
        {
            logger.LogInformation("Pipeline ended: {Ended}", ended);
            message = ended!;
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
