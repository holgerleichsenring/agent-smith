namespace AgentSmith.Contracts.Verify;

/// <summary>
/// 2026-11-17-d6fbc: a context whose verify block was derived from files that no longer hash to what was recorded.
/// The declared stages ran unchanged; this is evidence the declaration may be out of date, never that it is wrong.
/// </summary>
public sealed record VerifyStaleDerivation(string Repository, string Context, IReadOnlyList<string> Files);
