using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services;

/// <summary>
/// Default <see cref="IPlanOpenQuestionExtractor"/>. Publishes
/// <see cref="ContextKeys.PlanJson"/> whenever the plan is not Complete or
/// carries any <see cref="PlanOpenQuestion"/>; PlanOpenQuestionsHandler picks
/// it up downstream to post the question prompt to the tracker. A Complete
/// plan with no open questions is a no-op.
/// </summary>
public sealed class PlanOpenQuestionExtractor(ILogger<PlanOpenQuestionExtractor> logger)
    : IPlanOpenQuestionExtractor
{
    public int PublishSideChannel(Plan plan, string rawPlanJson, PipelineContext pipeline)
    {
        if (plan.Status == PlanStatus.Complete && plan.OpenQuestions.Count == 0)
            return 0;
        pipeline.Set(ContextKeys.PlanJson, rawPlanJson);
        logger.LogInformation(
            "Plan side-channel published: status={Status}, open_questions={Count}",
            plan.Status, plan.OpenQuestions.Count);
        return plan.OpenQuestions.Count;
    }
}
