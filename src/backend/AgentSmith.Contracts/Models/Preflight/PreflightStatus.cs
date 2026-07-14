namespace AgentSmith.Contracts.Models.Preflight;

/// <summary>
/// p0324: outcome class of a single preflight check. <see cref="Skip"/> means the
/// check could not run because the feature is not configured here — never a failure.
/// </summary>
public enum PreflightStatus
{
    Pass = 0,
    Fail = 1,
    Skip = 2,
}
