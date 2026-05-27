namespace AgentSmith.Application.Models;

/// <summary>
/// Five-state outcome for a skill call. Stable integer values for log/persist contracts.
/// </summary>
public enum SkillCallOutcome
{
    Ok = 0,
    Incomplete = 1,
    FailedParse = 2,
    FailedValidation = 3,
    FailedRuntime = 4
}
