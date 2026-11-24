using AgentSmith.Contracts.Pocs;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>2026-11-23-c81ee: the runs the lifetime pass's backoff reads — the ones the PoCs' LastRunId name, and the latest
/// run of a preset on a project (by id, which sorts by start). Read by id only: the times are compared in memory.</summary>
public sealed class PocLifetimeRunRepository(IUnitOfWork unitOfWork)
{
    public async Task<IReadOnlyDictionary<string, PocLastRun>> RunsAsync(IReadOnlyList<string> runIds, CancellationToken ct) =>
        runIds.Count == 0 ? new Dictionary<string, PocLastRun>()
            : await unitOfWork.Set<Run>().AsNoTracking().Where(r => runIds.Contains(r.Id))
                .Select(r => new PocLastRun(r.Id, r.Pipeline, r.Status, r.FinishedAt)).ToDictionaryAsync(r => r.Id, ct);

    public Task<PocLastRun?> LatestAsync(string project, string pipeline, CancellationToken ct) =>
        unitOfWork.Set<Run>().AsNoTracking().Where(r => r.Project == project && r.Pipeline == pipeline).OrderByDescending(r => r.Id)
            .Select(r => new PocLastRun(r.Id, r.Pipeline, r.Status, r.FinishedAt)).FirstOrDefaultAsync(ct);
}
