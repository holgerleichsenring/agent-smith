using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;

namespace AgentSmith.Application.Services.Admin;

/// <summary>
/// 2026-11-18-00eaa: the config kind 'secret-values' — a value stored in the admin area whose catalog secret is gone.
/// The assistant may only draft its deletion: a value is never drafted (it never passes the conversation), and a value
/// whose secret still exists is deleted in the secret drawer. What the draft and the model see is the name and when
/// it was set; no value, no person, no key id — so a re-wrap between draft and apply does not refuse a valid delete.
/// </summary>
public sealed class OrphanedSecretValues(ISecretVault vault)
{
    public const string Kind = "secret-values";

    public static bool Is(string kind) => string.Equals(kind, Kind, StringComparison.OrdinalIgnoreCase);

    /// <summary>The orphan as a draft shows it; null when no stored value of that name is an orphan.</summary>
    public async Task<string?> ReadAsync(IConfigStore store, string name, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(store);
        if (store.GetSecrets().Any(s => string.Equals(s.Id, name, StringComparison.OrdinalIgnoreCase))) return null;
        var state = (await vault.StatesAsync(ct)).FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
        return state is null ? null : ConfigEntityJson.Render(new { state.Name, state.SetAt });
    }

    /// <summary>The before of a drafted delete; refuses anything that is not the deletion of an orphan.</summary>
    public async Task<string> DraftAsync(IConfigStore store, string operation, string name, CancellationToken ct)
    {
        if (operation != ConfigChangeItem.Delete)
            throw new ConfigurationException("A stored secret value is set in the admin area's secret field, never drafted; only its deletion is.");
        return await ReadAsync(store, name, ct) ?? throw new ConfigurationException(
            $"'{name}' has no stored value whose catalog secret is gone; a value whose secret exists is deleted in the secret drawer.");
    }

    public Task<bool> DeleteAsync(string name, CancellationToken ct) => vault.DeleteAsync(name, ct);
}
