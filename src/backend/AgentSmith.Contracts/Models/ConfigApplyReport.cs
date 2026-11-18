namespace AgentSmith.Contracts.Models;

/// <summary>2026-11-17-f3a8c: what a config run did — every item applied, or the refusal and what was reverted.</summary>
public sealed record ConfigApplyReport(bool Applied, string Line);
