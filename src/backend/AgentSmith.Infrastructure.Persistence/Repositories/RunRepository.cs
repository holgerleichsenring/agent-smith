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
    public async Task<List<Run>> GetActiveRunsAsync(CancellationToken ct)
    {
        var runs = await unitOfWork.Set<Run>().AsNoTracking()
            .Where(r => r.FinishedAt == null)
            .OrderByDescending(r => r.Id) // sortable run id (p0156) = newest-first, SQLite-safe
            .ToListAsync(ct);
        await HydrateListChildrenAsync(runs, ct);
        return runs;
    }

    public async Task<List<Run>> GetRecentRunsAsync(int limit, CancellationToken ct)
    {
        var runs = await unitOfWork.Set<Run>().AsNoTracking()
            .Where(r => r.FinishedAt != null)
            .OrderByDescending(r => r.Id)
            .Take(limit)
            .ToListAsync(ct);
        await HydrateListChildrenAsync(runs, ct);
        return runs;
    }

    // The dashboard list cards render step progress, the repo list, and sandbox
    // + LLM-call counts — all child rows. Children are keyed by RunId (Ignored
    // on Run, no FK), so batch-load each set for the WHOLE page in one query and
    // group client-side, rather than N+1 per run.
    private async Task HydrateListChildrenAsync(List<Run> runs, CancellationToken ct)
    {
        if (runs.Count == 0) return;
        var ids = runs.Select(r => r.Id).ToList();
        var byId = runs.ToDictionary(r => r.Id);

        var steps = await unitOfWork.Set<RunStep>().AsNoTracking().Where(x => ids.Contains(x.RunId)).ToListAsync(ct);
        foreach (var g in steps.GroupBy(x => x.RunId)) byId[g.Key].Steps = g.ToList();

        var repos = await unitOfWork.Set<RunRepo>().AsNoTracking().Where(x => ids.Contains(x.RunId)).ToListAsync(ct);
        foreach (var g in repos.GroupBy(x => x.RunId)) byId[g.Key].Repos = g.ToList();

        var sandboxes = await unitOfWork.Set<RunSandbox>().AsNoTracking().Where(x => ids.Contains(x.RunId)).ToListAsync(ct);
        foreach (var g in sandboxes.GroupBy(x => x.RunId)) byId[g.Key].Sandboxes = g.ToList();

        var llmCalls = await unitOfWork.Set<RunLlmCall>().AsNoTracking().Where(x => ids.Contains(x.RunId)).ToListAsync(ct);
        foreach (var g in llmCalls.GroupBy(x => x.RunId)) byId[g.Key].LlmCalls = g.ToList();
    }

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
