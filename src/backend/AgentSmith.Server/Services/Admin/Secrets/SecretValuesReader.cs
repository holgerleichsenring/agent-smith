using AgentSmith.Contracts.Services;

namespace AgentSmith.Server.Services.Admin.Secrets;

/// <summary>
/// 2026-11-17-f3a8d: what the admin area and the admin assistant may know of the secret values: per catalog secret,
/// whether its environment variable holds a value and what is stored; nothing that is a value.
/// </summary>
public sealed class SecretValuesReader(IConfigStore store, ISecretVault vault)
{
    public async Task<SecretValuesView> ReadAsync(CancellationToken ct)
    {
        var stored = (await vault.StatesAsync(ct)).ToDictionary(s => s.Name, StringComparer.OrdinalIgnoreCase);
        var names = store.GetSecrets().Select(s => s.Id).ToList();
        var secrets = names.Select(name => new SecretValueState(
            name, !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(name)), stored.GetValueOrDefault(name))).ToList();
        var orphans = stored.Values.Where(s => !names.Contains(s.Name, StringComparer.OrdinalIgnoreCase)).ToList();
        return new SecretValuesView(vault.Unavailable is null, vault.Unavailable, secrets, orphans);
    }
}
