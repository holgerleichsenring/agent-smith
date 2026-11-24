using System.Security.Cryptography;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Persistence.Services.Secrets;

/// <summary>
/// 2026-11-23-933dg: the poc-scope rows opened — for the mask frame of a poc- project's run (every PoC the project holds;
/// a row that does not open is left out) and for one PoC's sealing step (each value in a redacting holder, or its name
/// with why it does not open). Nothing else opens them; the configuration load skips them. A scope per question.
/// </summary>
public sealed class PocSecretValues(
    IServiceScopeFactory scopes, MasterKeyRing keys, SecretEnvelope envelope, ILogger<PocSecretValues> logger)
    : IPocMaskValues, IPocVariableValues
{
    public async Task<IReadOnlyList<string>> ForProjectAsync(string project, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (!project.StartsWith(IPocProjects.Prefix, StringComparison.OrdinalIgnoreCase)) return [];
        using var scope = scopes.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var scopeTexts = (await unitOfWork.Set<Poc>().AsNoTracking().Where(p => p.Project == project).Select(p => p.Id).ToListAsync(ct))
            .Select(id => SecretScope.Poc(id).Text).ToList();
        if (scopeTexts.Count == 0) return [];
        var rows = await unitOfWork.Set<StoredSecret>().AsNoTracking().Where(s => scopeTexts.Contains(s.Scope)).ToListAsync(ct);
        return [.. rows.Select(row => Open(row, out _)).OfType<string>()];
    }

    public async Task<PocVariableValues> ReadAsync(long pocId, CancellationToken ct)
    {
        var text = SecretScope.Poc(pocId).Text;
        using var scope = scopes.CreateScope();
        var rows = await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().Set<StoredSecret>().AsNoTracking()
            .Where(s => s.Scope == text).ToListAsync(ct);
        var values = new Dictionary<string, SecretInput>(StringComparer.Ordinal);
        var unreadable = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var row in rows)
            if (Open(row, out var reason) is { } value) values[row.Name] = new SecretInput(value);
            else unreadable[row.Name] = reason!;
        return new PocVariableValues(values, unreadable);
    }

    private string? Open(StoredSecret row, out string? reason)
    {
        reason = null;
        if (keys.Find(row.KeyId) is not { } key)
        {
            reason = keys.Current is null ? "no master key is configured" : $"the master key {row.KeyId} that wrapped it is neither the current nor the previous one";
            return null;
        }
        try
        {
            return envelope.Open(StoredSecretNames.Envelope(row), StoredSecretRepository.Sealed(row), key);
        }
        catch (CryptographicException ex)
        {
            logger.LogWarning(ex, "The PoC variable {Name} of {Scope} did not open under key {KeyId}", row.Name, row.Scope, row.KeyId);
            reason = "it did not open under its key (tampered or wrapped by another key)";
            return null;
        }
    }
}
