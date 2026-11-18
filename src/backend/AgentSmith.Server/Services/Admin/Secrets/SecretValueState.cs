using AgentSmith.Contracts.Models;

namespace AgentSmith.Server.Services.Admin.Secrets;

/// <summary>
/// 2026-11-17-f3a8d: one catalog secret as the admin area shows it — whether the environment holds a value (it wins),
/// and the stored value's who, when and key. Never a value.
/// </summary>
public sealed record SecretValueState(string Name, bool FromEnvironment, StoredSecretState? Stored);
