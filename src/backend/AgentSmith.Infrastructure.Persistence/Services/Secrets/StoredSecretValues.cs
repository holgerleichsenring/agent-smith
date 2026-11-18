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
    public IReadOnlyDictionary<string, string> Load()
    {
        using var scope = scopes.CreateScope();
        var rows = scope.ServiceProvider.GetRequiredService<AgentSmithDbContext>().Set<StoredSecret>().ToList();
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var row in rows)
            if (Open(row) is { } value) values[row.Name] = value;
        return values;
    }

    private string? Open(StoredSecret row)
    {
        if (keys.Find(row.KeyId) is not { } key) return Unreadable(row, keys.Current is null
            ? $"no master key is configured ({keys.Problem ?? MasterKeyRing.Variable + " or " + MasterKeyRing.WrappedVariable})"
            : "the master key that wrapped it is neither the current nor the previous one");
        try
        {
            return envelope.Open(row.Name, StoredSecretRepository.Sealed(row), key);
        }
        catch (CryptographicException ex)
        {
            logger.LogWarning(ex, "The stored secret {Name} did not open under key {KeyId}", row.Name, row.KeyId);
            return Unreadable(row, "it did not open under its key (tampered or wrapped by another key)");
        }
    }

    private string? Unreadable(StoredSecret row, string reason)
    {
        findings.Record(new StartupFinding(StartupSubsystems.Configuration, StartupFindingSeverity.Advisory,
            $"The stored value of secret '{row.Name}' (key {row.KeyId}) cannot be read: {reason}. It resolves empty.",
            Field: "secrets"));
        return null;
    }
}
