namespace AgentSmith.Contracts.Series;

/// <summary>2026-11-17-0207d: one done criterion checked on the default branch, and what showed it.</summary>
public sealed record SeriesCriterionResult(string Criterion, bool Passed, string Evidence)
{
    /// <summary>2026-11-28-d1ffa: why the environment of the check kept it from being checked — a secret it lacks, a feed still
    /// unreachable after staging — and what would make it checkable; null for a criterion that was checked. Such a criterion
    /// neither holds nor fails: nothing is filed for it.</summary>
    public string? NotCheckable { get; init; }
}
