namespace AgentSmith.Contracts.Models;

/// <summary>2026-11-17-f3a8c: what a config run applies, and who it applies it for — the person who pressed Apply.</summary>
public sealed record ConfigChangeRequest(IReadOnlyList<ConfigChangeItem> Changes, string Person);
