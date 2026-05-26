using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Produces the open-questions side channel for a freshly parsed Plan. When a
/// plan declares NeedsUserInput (or has open questions even at Complete), the
/// raw JSON is needed downstream so PlanOpenQuestionsHandler can post the
/// questions to the tracker. The extractor decides whether to publish the
/// side-channel and, if so, what to publish. Splitting this off keeps
/// GeneratePlanHandler focused on plan generation rather than questions.
/// </summary>
public interface IPlanOpenQuestionExtractor
{
    /// <summary>
    /// Publishes the PlanJson side-channel into <paramref name="pipeline"/>
    /// when <paramref name="plan"/> declares NeedsUserInput or carries
    /// non-empty <see cref="Plan.OpenQuestions"/>. Returns the count of
    /// questions published (0 = no side-channel needed).
    /// </summary>
    int PublishSideChannel(Plan plan, string rawPlanJson, PipelineContext pipeline);
}
