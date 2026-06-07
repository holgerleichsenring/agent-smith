using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// Reads run list + detail over a SCOPED unit of work — the durable source the
/// dashboard reads after Redis is demoted to a nudge. Run children are keyed by
/// RunId (no FK relationship), so detail loads each set explicitly.
/// </summary>
public sealed class RunRepository(IUnitOfWork unitOfWork)
{
    public Task<List<Run>> GetActiveRunsAsync(CancellationToken ct) =>
        unitOfWork.Set<Run>().AsNoTracking()
            .Where(r => r.FinishedAt == null)
            .OrderByDescending(r => r.Id) // sortable run id (p0156) = newest-first, SQLite-safe
            .ToListAsync(ct);

    public Task<List<Run>> GetRecentRunsAsync(int limit, CancellationToken ct) =>
        unitOfWork.Set<Run>().AsNoTracking()
            .Where(r => r.FinishedAt != null)
            .OrderByDescending(r => r.Id)
            .Take(limit)
            .ToListAsync(ct);

    public async Task<Run?> GetRunDetailAsync(string runId, CancellationToken ct)
    {
        var run = await unitOfWork.Set<Run>().AsNoTracking().FirstOrDefaultAsync(r => r.Id == runId, ct);
        if (run is null) return null;
        run.Repos = await Children<RunRepo>(runId, ct);
        run.Steps = await Children<RunStep>(runId, ct);
        run.LlmCalls = await Children<RunLlmCall>(runId, ct);
        run.Sandboxes = await Children<RunSandbox>(runId, ct);
        run.Decisions = await Children<RunDecision>(runId, ct);
        return run;
    }

    private Task<List<T>> Children<T>(string runId, CancellationToken ct) where T : class =>
        unitOfWork.Set<T>().AsNoTracking().Where(x => EF.Property<string>(x, "RunId") == runId).ToListAsync(ct);
}
