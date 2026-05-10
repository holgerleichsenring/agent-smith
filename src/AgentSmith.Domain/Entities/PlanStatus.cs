namespace AgentSmith.Domain.Entities;

/// <summary>
/// Terminal state of a Plan-skill emission. <c>Complete</c> requires
/// open_questions to be empty; <c>NeedsUserInput</c> requires at least one
/// open question. Enforced by plan.schema.json + PlanOutputValidator.
/// </summary>
public enum PlanStatus
{
    Complete,
    NeedsUserInput
}
