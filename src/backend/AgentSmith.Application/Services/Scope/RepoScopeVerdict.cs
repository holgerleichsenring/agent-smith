namespace AgentSmith.Application.Services.Scope;

/// <summary>
/// p0386: one repository's verdict from the scope classifier. Confidence is the
/// classifier's certainty in THIS repo's verdict alone — never a global number,
/// so a confident exclusion survives unrelated doubt about another repo.
/// </summary>
public sealed record RepoScopeVerdict(
    string Name, bool Affected, double Confidence, string? Reason = null);
