namespace AgentSmith.Server.Services.Admin.Secrets;

/// <summary>2026-11-17-f3a8d: the admin area's secret values — the master key's state, each catalog secret, and stored
/// values whose catalog secret is gone. 2026-11-17-f3a8e: <paramref name="MasterKeySource"/> says where the master key
/// came from — the deployment decides it, so it is shown, never edited.</summary>
public sealed record SecretValuesView(
    bool MasterKeyPresent, string? MasterKeyProblem, IReadOnlyList<SecretValueState> Secrets,
    IReadOnlyList<AgentSmith.Contracts.Models.StoredSecretState> Orphans, string? MasterKeySource = null);
