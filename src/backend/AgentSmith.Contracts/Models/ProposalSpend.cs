namespace AgentSmith.Contracts.Models;

/// <summary>
/// 2026-11-10-d854a: what the turn that produced a proposal had spent when it was pushed, the cap it ran under, the tier
/// that sized that cap (null when none was stated or estimated) and whether the cap was exhausted.
/// </summary>
public sealed record ProposalSpend(decimal Usd, decimal? CapUsd, string? Tier, bool CapHit);
