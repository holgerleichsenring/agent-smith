using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// 2026-11-23-c81ee: what the poc-upload-purge run deletes. The stored PoC uploads are the ReferenceFile rows whose session
/// key starts with 'poc-upload:' (933db); their ids and CreatedAt are selected, the ages filtered in memory and the old ones
/// deleted set-based, as RunRetentionService does, because SQLite cannot translate a DateTimeOffset comparison. A deleted
/// PoC's variables are its 'poc:&lt;id&gt;' StoredSecret rows (deleted by the run through SecretWrite) and its plain values.
/// </summary>
public sealed class PocUploadPurgeRepository(IUnitOfWork unitOfWork)
{
    public const string UploadSessionPrefix = "poc-upload:";
    private const string PocScopePrefix = "poc:";

    public async Task<int> PurgeUploadsAsync(DateTimeOffset olderThan, CancellationToken ct)
    {
        var old = (await unitOfWork.Set<ReferenceFile>().AsNoTracking().Where(f => f.SessionId.StartsWith(UploadSessionPrefix))
                .Select(f => new { f.Id, f.CreatedAt }).ToListAsync(ct))
            .Where(f => f.CreatedAt < olderThan).Select(f => f.Id).ToList();
        if (old.Count == 0) return 0;
        return await unitOfWork.Set<ReferenceFile>().Where(f => old.Contains(f.Id)).ExecuteDeleteAsync(ct);
    }

    public async Task<IReadOnlyList<long>> DeletedWithVariablesAsync(CancellationToken ct)
    {
        var deleted = await unitOfWork.Set<Poc>().AsNoTracking().Where(p => p.State == AgentSmith.Contracts.Pocs.PocStates.Deleted)
            .Select(p => new { p.Id, HasPlain = p.PlainVariablesJson != null }).ToListAsync(ct);
        if (deleted.Count == 0) return [];
        var scopes = await unitOfWork.Set<StoredSecret>().AsNoTracking().Where(s => s.Scope.StartsWith(PocScopePrefix))
            .Select(s => s.Scope).Distinct().ToListAsync(ct);
        var withSecrets = scopes.Select(AgentSmith.Contracts.Models.SecretScope.Parse).Where(s => s?.PocId is not null)
            .Select(s => s!.PocId!.Value).ToHashSet();
        return [.. deleted.Where(p => p.HasPlain || withSecrets.Contains(p.Id)).Select(p => p.Id).Order()];
    }

    public Task ClearPlainVariablesAsync(long pocId, CancellationToken ct) =>
        unitOfWork.Set<Poc>().Where(p => p.Id == pocId).ExecuteUpdateAsync(set => set.SetProperty(p => p.PlainVariablesJson, (string?)null), ct);
}
