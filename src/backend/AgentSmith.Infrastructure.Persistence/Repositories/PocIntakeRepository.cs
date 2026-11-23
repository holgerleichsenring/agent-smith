using AgentSmith.Contracts.Pocs;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// 2026-11-23-933db: what a poc-deploy run's intake reads and writes — the revision the seeds name and its PoC, the
/// stored archive, and the outcome. A revision already validated or refused is never written again, and a refused one
/// turns a PoC that is still importing (never ready) refused.
/// </summary>
public sealed class PocIntakeRepository(IUnitOfWork unitOfWork, ReferenceSetRepository uploads)
{
    public async Task<PocIntakeSubject?> ReadAsync(long pocId, int revision, CancellationToken ct)
    {
        var poc = await unitOfWork.Set<Poc>().AsNoTracking().FirstOrDefaultAsync(p => p.Id == pocId, ct);
        var row = await unitOfWork.Set<PocRevision>().AsNoTracking()
            .FirstOrDefaultAsync(r => r.PocId == pocId && r.Number == revision, ct);
        return poc is null || row is null
            ? null
            : new PocIntakeSubject(poc.Id, poc.Name, poc.State, row.Number, row.State, row.UploadSession, row.UploadId);
    }

    public async Task<byte[]?> ArchiveAsync(string uploadSession, string uploadId, CancellationToken ct) =>
        (await uploads.FilesAsync(uploadSession, uploadId, ct)) is [var file, ..] ? file.Content : null;

    public async Task RecordAsync(PocIntakeOutcome outcome, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        var written = await unitOfWork.Set<PocRevision>()
            .Where(r => r.PocId == outcome.PocId && r.Number == outcome.Revision && r.State == PocRevisionStates.Admitted)
            .ExecuteUpdateAsync(set => set
                .SetProperty(r => r.State, outcome.State).SetProperty(r => r.RunId, outcome.RunId)
                .SetProperty(r => r.ManifestJson, outcome.ManifestJson).SetProperty(r => r.Port, outcome.Port)
                .SetProperty(r => r.Entrypoint, outcome.Entrypoint).SetProperty(r => r.BaseImage, outcome.BaseImage)
                .SetProperty(r => r.Reason, outcome.Reason), ct);
        if (written > 0 && outcome.State == PocRevisionStates.Refused)
            await unitOfWork.Set<Poc>().Where(p => p.Id == outcome.PocId && p.State == PocStates.Importing)
                .ExecuteUpdateAsync(set => set.SetProperty(p => p.State, PocStates.Refused), ct);
    }
}
