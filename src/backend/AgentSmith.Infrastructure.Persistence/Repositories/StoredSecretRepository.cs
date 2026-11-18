using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Services.Secrets;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// 2026-11-17-f3a8d: the secret store over a scoped unit of work. A set seals the value under the current master key
/// and replaces the row; nothing here returns a value — <see cref="StoredSecretValues"/> opens them for a load.
/// </summary>
public sealed class StoredSecretRepository(IUnitOfWork unitOfWork, MasterKeyRing keys, SecretEnvelope envelope, TimeProvider time)
    : ISecretVault
{
    public string? Unavailable => keys.Problem ?? (keys.Current is null
        ? $"No master key is configured: set {MasterKeyRing.Variable} (base64 of 32 bytes) or {MasterKeyRing.Variable}_FILE, "
          + $"or {MasterKeyRing.WrappedVariable} with {MasterKeyRing.VaultKeyVariable} (Azure Key Vault)."
        : null);

    public string? KeySource => keys.Source;

    public async Task SetAsync(string name, SecretInput value, string setBy, CancellationToken ct)
    {
        var key = keys.Current ?? throw new InvalidOperationException(Unavailable);
        var row = await Row(name, ct) ?? Added(name);
        Apply(row, envelope.Seal(Key(name), value.Reveal(), key));
        (row.SetBy, row.SetAt, row.Version) = (setBy, time.GetUtcNow(), SecretEnvelope.Version);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<bool> DeleteAsync(string name, CancellationToken ct) =>
        await unitOfWork.Set<StoredSecret>().Where(s => s.Name == Key(name)).ExecuteDeleteAsync(ct) > 0;

    public async Task<int> RewrapAsync(CancellationToken ct)
    {
        var to = keys.Current ?? throw new InvalidOperationException(Unavailable);
        var stale = await unitOfWork.Set<StoredSecret>().Where(s => s.KeyId != to.Id).ToListAsync(ct);
        var moved = 0;
        foreach (var row in stale)
        {
            if (keys.Find(row.KeyId) is not { } from) continue;
            Apply(row, envelope.Rewrap(row.Name, Sealed(row), from, to));
            moved++;
        }
        await unitOfWork.SaveChangesAsync(ct);
        return moved;
    }

    public async Task<IReadOnlyList<StoredSecretState>> StatesAsync(CancellationToken ct) =>
        [.. (await unitOfWork.Set<StoredSecret>().AsNoTracking().ToListAsync(ct)).Select(r => new StoredSecretState(
            r.Name, r.SetBy, r.SetAt, r.KeyId, r.KeyId == keys.Current?.Id, keys.Find(r.KeyId) is not null))];

    internal static SealedSecret Sealed(StoredSecret row) => new(row.Ciphertext, row.Nonce, row.WrappedKey, row.WrapNonce, row.KeyId);

    private static void Apply(StoredSecret row, SealedSecret sealedSecret) =>
        (row.Ciphertext, row.Nonce, row.WrappedKey, row.WrapNonce, row.KeyId) =
        (sealedSecret.Ciphertext, sealedSecret.Nonce, sealedSecret.WrappedKey, sealedSecret.WrapNonce, sealedSecret.KeyId);

    private Task<StoredSecret?> Row(string name, CancellationToken ct) =>
        unitOfWork.Set<StoredSecret>().FirstOrDefaultAsync(s => s.Name == Key(name), ct);

    private StoredSecret Added(string name)
    {
        var row = new StoredSecret { Name = Key(name) };
        unitOfWork.Add(row);
        return row;
    }

    private static string Key(string name) => name.ToLowerInvariant();
}
