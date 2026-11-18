using System.Security.Cryptography;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Persistence.Services.Secrets;

/// <summary>
/// 2026-11-17-f3a8d: the stored secret values, opened once per configuration load — the load is what runs and the
/// masker both read, so they see one generation. A row the running keys cannot open resolves to nothing and is said
/// as a configuration finding naming the secret and its key id; a missing key with rows stored is said the same way.
/// The load reads through a scope of its own, because the loader is called from places that hold none.
/// </summary>
public sealed class StoredSecretValues(
    IServiceScopeFactory scopes, MasterKeyRing keys, SecretEnvelope envelope, IStartupFindings findings,
    ILogger<StoredSecretValues> logger) : IStoredSecretValues
{
    public StoredSecretLoad Load()
    {
        using var scope = scopes.CreateScope();
        var rows = scope.ServiceProvider.GetRequiredService<AgentSmithDbContext>().Set<StoredSecret>().ToList();
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var unreadable = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var row in rows)
            if (Open(row, unreadable) is { } value) values[row.Name] = value;
        return new StoredSecretLoad(values, unreadable);
    }

    private string? Open(StoredSecret row, Dictionary<string, string> unreadable)
    {
        if (keys.Find(row.KeyId) is not { } key) return Unreadable(row, unreadable, keys.Current is null
            ? $"no master key is configured ({keys.Problem ?? MasterKeyRing.Variable + " or " + MasterKeyRing.WrappedVariable})"
            : "the master key that wrapped it is neither the current nor the previous one");
        try
        {
            return envelope.Open(row.Name, StoredSecretRepository.Sealed(row), key);
        }
        catch (CryptographicException ex)
        {
            logger.LogWarning(ex, "The stored secret {Name} did not open under key {KeyId}", row.Name, row.KeyId);
            return Unreadable(row, unreadable, "it did not open under its key (tampered or wrapped by another key)");
        }
    }

    // 2026-11-18-78efb: a webhook or Slack signing secret left unreadable refuses every delivery, so the finding says so.
    private string? Unreadable(StoredSecret row, Dictionary<string, string> unreadable, string reason)
    {
        unreadable[row.Name] = $"{reason} (key {row.KeyId})";
        findings.Record(new StartupFinding(StartupSubsystems.Configuration, StartupFindingSeverity.Advisory,
            $"The stored value of secret '{row.Name}' (key {row.KeyId}) cannot be read: {reason}. It resolves empty; "
            + "a webhook or Slack signing secret in this state refuses every delivery until its value is set again.",
            Field: "secrets"));
        return null;
    }
}
