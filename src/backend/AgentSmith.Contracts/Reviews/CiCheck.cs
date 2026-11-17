namespace AgentSmith.Contracts.Reviews;

/// <summary>2026-11-17-3435a: one check as a provider read it, before <see cref="CiVerdict"/> decides the head.</summary>
public sealed record CiCheck(string Name, CiCheckOutcome Outcome, bool Required, DateTimeOffset? At);
