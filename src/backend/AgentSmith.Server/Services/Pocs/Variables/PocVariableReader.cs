using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Server.Services.Pocs.Variables;

/// <summary>2026-11-23-933dg: a PoC's variables for GET — a secret's name, setter and time from the store's states (never
/// its value), a plain one's from the Poc row with its value.</summary>
internal sealed class PocVariableReader(ISecretVault vault, IPocVariableStore store)
{
    public async Task<PocVariablesView?> ReadAsync(long pocId, CancellationToken ct)
    {
        if (await store.ReadAsync(pocId, ct) is not { } poc) return null;
        var secrets = (await vault.StatesAsync(SecretScope.Poc(pocId), ct))
            .Select(s => new PocVariableView(s.Name, PocVariableKinds.Secret, s.SetBy, s.SetAt, null));
        var plain = poc.Plain.Select(p => new PocVariableView(p.Name, PocVariableKinds.Plain, p.SetBy, p.SetAt, p.Value));
        return new PocVariablesView([.. secrets.Concat(plain).OrderBy(v => v.Name, StringComparer.Ordinal)]);
    }
}
