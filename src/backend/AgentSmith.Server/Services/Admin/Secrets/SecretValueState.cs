using AgentSmith.Contracts.Models;

namespace AgentSmith.Server.Services.Admin.Secrets;

/// <summary>
/// 2026-11-17-f3a8d: one catalog secret as the admin area shows it — whether the environment holds a value (it wins),
/// and the stored value's who, when and key. Never a value. 2026-11-18-9ae8: <paramref name="Variable"/> is the
/// environment variable the secret reads, which is its reference rather than always its name.
/// </summary>
public sealed record SecretValueState(string Name, string Variable, bool FromEnvironment, StoredSecretState? Stored);
