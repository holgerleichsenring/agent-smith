using AgentSmith.Contracts.Pocs;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// 2026-11-23-933db: the Poc and PocRevision rows an import writes and the reads that decide them. A live PoC is one not
/// refused or deleted (PocStates.IsLive, spelled for SQL). A new row's per-person count is re-checked after the insert,
/// in the same transaction, which rolls back when it is over; a unique-index violation on (Target, LiveName) from a
/// concurrent import reads as a taken name.
/// </summary>
public sealed class PocImportRepository(IUnitOfWork unitOfWork, IUniqueViolationTranslator violations)
{
    public Task<PocRevision?> RevisionOfUploadAsync(string uploadId, CancellationToken ct) =>
        unitOfWork.Set<PocRevision>().AsNoTracking().FirstOrDefaultAsync(r => r.UploadId == uploadId, ct);

    public Task<PocRevision?> LatestValidatedAsync(long pocId, CancellationToken ct) =>
        unitOfWork.Set<PocRevision>().AsNoTracking()
            .Where(r => r.PocId == pocId && r.State == PocRevisionStates.Validated)
            .OrderByDescending(r => r.Number).FirstOrDefaultAsync(ct);

    /// <summary>The PoC holding <paramref name="name"/> on <paramref name="target"/>, deleted ones left out.</summary>
    public Task<Poc?> HolderOfAsync(string target, string name, CancellationToken ct) =>
        unitOfWork.Set<Poc>().AsNoTracking().FirstOrDefaultAsync(p => p.Target == target && p.LiveName == name, ct);

    public Task<int> LiveCountAsync(string owner, string target, CancellationToken ct) =>
        unitOfWork.Set<Poc>().CountAsync(
            p => p.Owner == owner && p.Target == target && p.State != PocStates.Refused && p.State != PocStates.Deleted, ct);

    /// <summary>Whether <paramref name="owner"/> owns a PoC in <paramref name="project"/>.</summary>
    public Task<bool> OwnsInProjectAsync(string owner, string project, CancellationToken ct) =>
        unitOfWork.Set<Poc>().AnyAsync(p => p.Owner == owner && p.Project == project, ct);

    /// <summary>Inserts <paramref name="poc"/>; with a <paramref name="limit"/>, rolls back when the owner's live PoCs on
    /// the target then exceed it.</summary>
    public async Task<PocInsertOutcome> InsertAsync(Poc poc, int? limit, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(poc);
        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        var entry = unitOfWork.Add(poc);
        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (violations.IsUniqueViolation(ex))
        {
            entry.State = EntityState.Detached;
            return PocInsertOutcome.NameTaken;
        }
        if (limit is { } most && await LiveCountAsync(poc.Owner, poc.Target, ct) > most)
        {
            await transaction.RollbackAsync(ct);
            entry.State = EntityState.Detached;
            return PocInsertOutcome.OverLimit;
        }
        await transaction.CommitAsync(ct);
        return PocInsertOutcome.Inserted;
    }

    /// <summary>A refused PoC of the same owner and name, imported again, goes back to importing.</summary>
    public Task ReviveAsync(long pocId, CancellationToken ct) =>
        unitOfWork.Set<Poc>().Where(p => p.Id == pocId && p.State == PocStates.Refused)
            .ExecuteUpdateAsync(set => set.SetProperty(p => p.State, PocStates.Importing), ct);

    /// <summary>The PoC's next revision, admitted; the (PocId, Number) index refuses a number taken concurrently.</summary>
    public async Task<PocRevision> AddRevisionAsync(long pocId, string session, string uploadId, string actor, CancellationToken ct)
    {
        var latest = await unitOfWork.Set<PocRevision>().Where(r => r.PocId == pocId).MaxAsync(r => (int?)r.Number, ct) ?? 0;
        var revision = new PocRevision
        {
            PocId = pocId, Number = latest + 1, UploadSession = session, UploadId = uploadId,
            State = PocRevisionStates.Admitted, Actor = actor,
        };
        unitOfWork.Add(revision);
        await unitOfWork.SaveChangesAsync(ct);
        return revision;
    }

    /// <summary>Removes a revision no run took, so a lost claim burns no number.</summary>
    public Task DeleteUnclaimedRevisionAsync(long revisionId, CancellationToken ct) =>
        unitOfWork.Set<PocRevision>().Where(r => r.Id == revisionId && r.RunId == null).ExecuteDeleteAsync(ct);

    /// <summary>The upload ids of <paramref name="session"/> a revision names — imported, so never replaced.</summary>
    public async Task<IReadOnlyList<string>> ImportedUploadIdsAsync(string session, CancellationToken ct) =>
        await unitOfWork.Set<PocRevision>().AsNoTracking().Where(r => r.UploadSession == session)
            .Select(r => r.UploadId).ToListAsync(ct);
}
