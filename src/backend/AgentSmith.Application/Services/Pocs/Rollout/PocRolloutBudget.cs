namespace AgentSmith.Application.Services.Pocs.Rollout;

/// <summary>2026-11-23-c81ec: until when a follow may wait, and the bound that set it — with its value and key — for the
/// sentence a late follow fails with.</summary>
public sealed record PocRolloutBudget(DateTimeOffset Until, string Bound);
