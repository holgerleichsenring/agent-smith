using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Persistence.Entities;

namespace AgentSmith.Infrastructure.Persistence.Services.Secrets;

/// <summary>
/// 2026-11-23-933dg: how a name is keyed per scope. An installation secret keeps f3a8d's rule — lower-cased, and the
/// envelope binds that name; a PoC variable keeps its name as written (the rule makes it upper case, and envFrom reads
/// it as written) and the envelope binds 'poc:&lt;id&gt;\0&lt;NAME&gt;', so its ciphertext opens under no other row.
/// </summary>
public static class StoredSecretNames
{
    /// <summary>The Name column's value of <paramref name="name"/> in <paramref name="scope"/>.</summary>
    public static string Key(SecretScope scope, string name)
    {
        ArgumentNullException.ThrowIfNull(scope);
        return scope.IsInstallation ? name.ToLowerInvariant() : name;
    }

    /// <summary>The name both associated data of <paramref name="row"/> bind.</summary>
    public static string Envelope(StoredSecret row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return row.Scope == SecretScope.InstallationText ? row.Name : $"{row.Scope}\0{row.Name}";
    }
}
