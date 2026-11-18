namespace AgentSmith.Contracts.Models;

/// <summary>2026-11-17-f3a8c: an admin turn ends in configuration changes, applied in order by a config run on Apply.</summary>
public sealed record ConfigChangeOutcome(IReadOnlyList<ConfigChangeItem> Changes) : OutcomeProposal;
