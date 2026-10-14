namespace AgentSmith.Contracts.Specs;

/// <summary>2026-10-13-8762j: the most lines <paramref name="Path"/> may hold once member
/// <paramref name="Spec"/> is built.</summary>
public sealed record SeriesContractBudget(string Path, string Spec, int MaxLines);
