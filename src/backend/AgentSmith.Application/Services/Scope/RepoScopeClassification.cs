namespace AgentSmith.Application.Services.Scope;

/// <summary>
/// p0331: the parsed ticket→repo classifier reply. <see cref="Repos"/> is the
/// classifier's affected-repo list (raw names, validated against the run's repo
/// list by <see cref="RepoScopeEvaluator"/>); <see cref="Confidence"/> is its
/// certainty that the OMITTED repos are unaffected (0..1).
/// </summary>
public sealed record RepoScopeClassification(
    IReadOnlyList<string> Repos, double Confidence, string? Rationale);
