namespace AgentSmith.Contracts.Models;

/// <summary>2026-11-12-cd4ca: one spec of a design turn's draft; <paramref name="Parent"/> marks the epic's parent.</summary>
public sealed record DraftSpec(SpecDraft Draft, bool Parent);
