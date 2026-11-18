namespace AgentSmith.Contracts.Models;

/// <summary>2026-11-17-f3a8c: what a config run applies, and who it applies it for — the person who pressed Apply.</summary>
/// <param name="MayWriteSecrets">2026-11-18-78efa: whether that person held secrets.write when they pressed Apply; a
/// stored secret value is deleted only for one who did.</param>
public sealed record ConfigChangeRequest(IReadOnlyList<ConfigChangeItem> Changes, string Person, bool MayWriteSecrets);
