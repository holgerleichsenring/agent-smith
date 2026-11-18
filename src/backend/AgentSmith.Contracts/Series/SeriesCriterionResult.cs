namespace AgentSmith.Contracts.Series;

/// <summary>2026-11-17-0207d: one done criterion checked on the default branch, and what showed it.</summary>
public sealed record SeriesCriterionResult(string Criterion, bool Passed, string Evidence);
