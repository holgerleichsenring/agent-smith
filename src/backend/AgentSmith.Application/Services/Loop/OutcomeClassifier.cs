using AgentSmith.Application.Models;

namespace AgentSmith.Application.Services.Loop;

/// <summary>
/// Pure mapping function from runtime state to <see cref="SkillCallOutcome"/>.
/// Stateless; registered as a DI singleton.
/// </summary>
public sealed class OutcomeClassifier
{
    public SkillCallOutcome Classify(ClassificationInput input)
    {
        if (input.CaughtException is not null)
            return SkillCallOutcome.FailedRuntime;

        if (input.LimitHit is { } limit && !limit.IsContinue)
            return input.ResponsePresent
                ? SkillCallOutcome.Incomplete
                : SkillCallOutcome.FailedRuntime;

        if (!input.ParseSuccess)
            return SkillCallOutcome.FailedParse;

        if (!input.ValidationSuccess)
            return SkillCallOutcome.FailedValidation;

        return SkillCallOutcome.Ok;
    }
}
