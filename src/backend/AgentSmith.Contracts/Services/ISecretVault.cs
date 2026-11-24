using AgentSmith.Contracts.Models;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-11-17-f3a8d: the installation's own secret values, envelope-encrypted under a master key that is not in the
/// database. Write-only: nothing here returns a value. 2026-11-23-933dg: every value has a scope — the installation's
/// catalog secrets, or one PoC's variables — and set, delete and states act within one scope only.
/// </summary>
public interface ISecretVault
{
    /// <summary>Why no value can be written or read now (no or a malformed master key); null when the key is there.</summary>
    string? Unavailable { get; }

    /// <summary>2026-11-17-f3a8e: where the current master key came from (environment, or the Azure Key Vault key that
    /// unwrapped it), naming the variable; null without a key. Never the key.</summary>
    string? KeySource { get; }

    Task SetAsync(SecretScope scope, string name, SecretInput value, string setBy, CancellationToken ct);

    Task<bool> DeleteAsync(SecretScope scope, string name, CancellationToken ct);

    /// <summary>Re-wraps every value a previous master key wraps under the current one; the count re-wrapped.</summary>
    Task<int> RewrapAsync(CancellationToken ct);

    Task<IReadOnlyList<StoredSecretState>> StatesAsync(SecretScope scope, CancellationToken ct);
}
