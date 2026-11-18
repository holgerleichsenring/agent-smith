namespace AgentSmith.Contracts.Models;

/// <summary>
/// 2026-11-17-f3a8d: a stored secret value as the admin area may see it — who set it and when, which master key
/// wraps it, and whether the running key can open it. Never the value.
/// </summary>
public sealed record StoredSecretState(string Name, string SetBy, DateTimeOffset SetAt, string KeyId, bool KeyCurrent, bool Readable);
