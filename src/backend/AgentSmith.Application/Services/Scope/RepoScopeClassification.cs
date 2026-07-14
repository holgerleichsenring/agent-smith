namespace AgentSmith.Application.Services.Scope;

/// <summary>
/// p0331: the parsed ticket→repo classifier reply. <see cref="Repos"/> is the
/// classifier's affected-repo list (raw names, validated against the run's repo
/// list by <see cref="RepoScopeEvaluator"/>); <see cref="Confidence"/> is its
/// certainty that the OMITTED repos are unaffected (0..1).
/// p0336b: <see cref="Contexts"/> is the optional per-repo affected-CONTEXT map
/// (repo name → context names) — the classifier's finer-grained verdict used to
/// drop a whole sandbox within a kept repo. Null = no context-level verdict, so
/// <see cref="ContextScopeEvaluator"/> keeps ALL contexts of every kept repo.
/// </summary>
public sealed record RepoScopeClassification(
    IReadOnlyList<string> Repos, double Confidence, string? Rationale,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? Contexts = null);
