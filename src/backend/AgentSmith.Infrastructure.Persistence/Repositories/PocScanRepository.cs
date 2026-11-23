using AgentSmith.Contracts.Pocs;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// 2026-11-23-933dc: what a poc-deploy run's code scan and commit read and write on a revision. A blocked verdict blocks a
/// PoC that never reached ready (importing or blocked); a passing one sets a blocked PoC back to importing; a ready PoC
/// keeps serving its current revision. Overruled ids are appended under their stage, never replaced.
/// </summary>
public sealed class PocScanRepository(IUnitOfWork unitOfWork)
{
    public async Task<PocScanSubject?> ReadAsync(long pocId, int revision, CancellationToken ct)
    {
        var poc = await unitOfWork.Set<Poc>().AsNoTracking().FirstOrDefaultAsync(p => p.Id == pocId, ct);
        var row = await unitOfWork.Set<PocRevision>().AsNoTracking().FirstOrDefaultAsync(r => r.PocId == pocId && r.Number == revision, ct);
        if (poc is null || row is null) return null;
        var latest = await unitOfWork.Set<PocRevision>().Where(r => r.PocId == pocId).MaxAsync(r => r.Number, ct);
        return new PocScanSubject(poc.Id, poc.Name, poc.State, poc.Folder, row.Number, row.State, latest, row.UploadSession,
            row.UploadId, row.ManifestJson, row.VerdictJson, row.OverruledIdsJson, row.CommitSha);
    }

    /// <summary>The revision row and the PoC's latest revision number — what the revision routes read.</summary>
    public async Task<(PocRevision Row, int Latest)?> RowAsync(long pocId, int revision, CancellationToken ct)
    {
        var row = await unitOfWork.Set<PocRevision>().AsNoTracking().FirstOrDefaultAsync(r => r.PocId == pocId && r.Number == revision, ct);
        return row is null ? null : (row, await unitOfWork.Set<PocRevision>().Where(r => r.PocId == pocId).MaxAsync(r => r.Number, ct));
    }

    public async Task AppendOverruledAsync(long pocId, int revision, string stage, IReadOnlyList<string> ids, CancellationToken ct)
    {
        var row = await unitOfWork.Set<PocRevision>().FirstAsync(r => r.PocId == pocId && r.Number == revision, ct);
        row.OverruledIdsJson = PocOverruledIds.FromJson(row.OverruledIdsJson).Append(stage, ids).ToJson();
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task RecordVerdictAsync(PocScanOutcome outcome, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        var blocked = outcome.State == PocRevisionStates.Blocked;
        var row = await unitOfWork.Set<PocRevision>().FirstAsync(r => r.PocId == outcome.PocId && r.Number == outcome.Revision, ct);
        (row.State, row.VerdictJson) = (outcome.State, outcome.VerdictJson);
        if (blocked) row.Explanation = outcome.Explanation;
        await unitOfWork.SaveChangesAsync(ct);
        var (from, to) = blocked ? (new[] { PocStates.Importing, PocStates.Blocked }, PocStates.Blocked) : ([PocStates.Blocked], PocStates.Importing);
        await unitOfWork.Set<Poc>().Where(p => p.Id == outcome.PocId && from.Contains(p.State))
            .ExecuteUpdateAsync(set => set.SetProperty(p => p.State, to), ct);
    }

    public Task RecordCommitAsync(long pocId, int revision, string commitSha, CancellationToken ct) =>
        unitOfWork.Set<PocRevision>().Where(r => r.PocId == pocId && r.Number == revision)
            .ExecuteUpdateAsync(set => set.SetProperty(r => r.CommitSha, commitSha), ct);
}
