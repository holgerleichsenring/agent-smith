using System.Text.Json;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;

namespace AgentSmith.Server.Services.Admin.Secrets;

/// <summary>
/// 2026-11-17-f3a8d: what the admin area and the admin assistant may know of the secret values: per catalog secret,
/// whether its environment variable holds a value and what is stored; nothing that is a value.
/// 2026-11-18-9ae8: the environment is asked for the variable the secret REFERENCES (its stored <c>${NAME}</c>), not
/// for its name — a secret anthropic_api_key reading ${ANTHROPIC_API_KEY} comes from the environment when
/// ANTHROPIC_API_KEY holds a value.
/// </summary>
public sealed class SecretValuesReader(
    IConfigStore store, ISecretVault vault, IConfigDocumentStore documents, Func<string, string?>? environment = null)
{
    private readonly Func<string, string?> _environment = environment ?? Environment.GetEnvironmentVariable;

    public async Task<SecretValuesView> ReadAsync(CancellationToken ct)
    {
        var stored = (await vault.StatesAsync(ct)).ToDictionary(s => s.Name, StringComparer.OrdinalIgnoreCase);
        var names = store.GetSecrets().Select(s => s.Id).ToList();
        var variables = Variables();
        var secrets = names.Select(name => State(name, variables.GetValueOrDefault(name) ?? name, stored.GetValueOrDefault(name))).ToList();
        var orphans = stored.Values.Where(s => !names.Contains(s.Name, StringComparer.OrdinalIgnoreCase)).ToList();
        return new SecretValuesView(vault.Unavailable is null, vault.Unavailable, secrets, orphans, vault.KeySource);
    }

    private SecretValueState State(string name, string variable, StoredSecretState? stored) =>
        new(name, variable, !string.IsNullOrEmpty(_environment(variable)), stored);

    private Dictionary<string, string> Variables()
    {
        var variables = new Dictionary<string, string>(ConfigNames.Comparer);
        foreach (var row in documents.LoadAll().Where(r => r.Type == ConfigDocTypes.Secret))
            if (ConfigSecretReferences.Referenced(Text(row.Doc)) is { } variable) variables[row.Id] = variable;
        return variables;
    }

    private static string? Text(string doc)
    {
        try { return JsonSerializer.Deserialize<string>(doc); }
        catch (JsonException) { return null; }
    }
}
