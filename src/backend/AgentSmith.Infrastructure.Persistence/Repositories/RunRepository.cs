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

    // p0355: cursor page for the runs-list "load more" — finished runs OLDER than
    // `before`, newest-first, so the operator reaches every run beyond the recent
    // window (not just the newest ~50). SQLite cannot compare DateTimeOffset in SQL
    // (same constraint as the cancel/wall-time scans), so the child-free run rows
    // are ordered by the sortable id and the cursor is applied client-side; only the
    // page's children are then hydrated.
    public async Task<List<Run>> GetRunsBeforeAsync(DateTimeOffset before, int limit, CancellationToken ct)
    {
        var finished = await unitOfWork.Set<Run>().AsNoTracking()
            .Where(r => r.FinishedAt != null)
            .OrderByDescending(r => r.Id)
            .ToListAsync(ct);
        var page = finished.Where(r => r.StartedAt < before).Take(limit).ToList();
        await HydrateListChildrenAsync(page, ct);
        return page;
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

    // p0330: SYNCHRONOUS cancel persistence — the endpoint writes the flag + the
    // durable kill deadline BEFORE returning (the event stays for fanout, but the
    // projector draining it later is not the state of record any more). Targeted
    // ExecuteUpdate; a repeat cancel keeps the FIRST deadline so the grace window
    // never re-extends. Returns true when a live (non-terminal) row carries the
    // flag afterwards.
    public async Task<bool> MarkCancelRequestedAsync(
        string runId, string reason, DateTimeOffset killDeadline, CancellationToken ct)
    {
        var updated = await unitOfWork.Set<Run>()
            .Where(r => r.Id == runId && r.FinishedAt == null && !r.CancelRequested)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.CancelRequested, true)
                .SetProperty(r => r.CancelReason, reason)
                .SetProperty(r => r.CancelDeadlineAt, killDeadline), ct);
        if (updated > 0) return true;
        // Already flagged (repeat click) still counts as a live cancel target.
        return await unitOfWork.Set<Run>().AsNoTracking()
            .AnyAsync(r => r.Id == runId && r.FinishedAt == null && r.CancelRequested, ct);
    }

    // p0330: pre-start gate read (queue consumer / capacity pump).
    public Task<bool> IsCancelRequestedAsync(string runId, CancellationToken ct) =>
        unitOfWork.Set<Run>().AsNoTracking()
            .AnyAsync(r => r.Id == runId && r.FinishedAt == null && r.CancelRequested, ct);

    // p0330: enforcement candidates — cancel requested, not terminal, deadline
    // elapsed. SQLite cannot translate a DateTimeOffset comparison, so the (small)
    // flagged set is filtered client-side, same as ActiveRunRepository.FindStaleAsync.
    public async Task<IReadOnlyList<Run>> GetCancelEnforcementCandidatesAsync(
        DateTimeOffset now, CancellationToken ct)
    {
        var flagged = await unitOfWork.Set<Run>().AsNoTracking()
            .Where(r => r.CancelRequested && r.FinishedAt == null && r.CancelDeadlineAt != null)
            .ToListAsync(ct);
        return flagged.Where(r => r.CancelDeadlineAt <= now).ToList();
    }

    // p0348: wall-time backstop candidates — a RUNNING run that outran the
    // ceiling and is not already flagged. Only status="running" qualifies: a
    // "queued" (waiting for capacity) or "waiting_for_input" (parked on a
    // question) run is legitimately idle, not hung. Same client-side timespan
    // filter as above (SQLite can't compare DateTimeOffset in SQL).
    public async Task<IReadOnlyList<Run>> GetWallTimeOverdueRunsAsync(
        TimeSpan maxWallTime, DateTimeOffset now, CancellationToken ct)
    {
        var running = await unitOfWork.Set<Run>().AsNoTracking()
            .Where(r => r.FinishedAt == null && !r.CancelRequested && r.Status == "running")
            .ToListAsync(ct);
        return running.Where(r => now - r.StartedAt > maxWallTime).ToList();
    }
}
