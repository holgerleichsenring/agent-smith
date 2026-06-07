using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// Reads run list + detail back from the relational store — the durable source
/// the dashboard reads after p0246e demotes Redis to a nudge. Survives a process
/// restart AND a Redis flush, since the facts live in the DB, not the stream.
/// </summary>
public sealed class DbRunStore(IDbContextFactory<AgentSmithDbContext> contextFactory)
{
    public async Task<IReadOnlyList<Run>> GetActiveRunsAsync(CancellationToken cancellationToken)
    {
        await using var ctx = await contextFactory.CreateDbContextAsync(cancellationToken);
        // Order by the run id, NOT StartedAt: the id is a sortable ISO-8601+hex
        // value (p0156), so id-desc IS newest-first — and it's an indexed string
        // column SQLite can ORDER BY (it cannot ORDER BY a DateTimeOffset).
        return await ctx.Runs.AsNoTracking()
            .Where(r => r.FinishedAt == null)
            .OrderByDescending(r => r.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Run>> GetRecentRunsAsync(int limit, CancellationToken cancellationToken)
    {
        await using var ctx = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await ctx.Runs.AsNoTracking()
            .Where(r => r.FinishedAt != null)
            .OrderByDescending(r => r.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<Run?> GetRunDetailAsync(string runId, CancellationToken cancellationToken)
    {
        await using var ctx = await contextFactory.CreateDbContextAsync(cancellationToken);
        var run = await ctx.Runs.AsNoTracking().FirstOrDefaultAsync(r => r.Id == runId, cancellationToken);
        if (run is null) return null;

        // The children are keyed by RunId (no FK relationship), so load each set
        // explicitly and fill the in-memory collections.
        run.Repos = await ctx.RunRepos.AsNoTracking().Where(x => x.RunId == runId).ToListAsync(cancellationToken);
        run.Steps = await ctx.RunSteps.AsNoTracking().Where(x => x.RunId == runId).ToListAsync(cancellationToken);
        run.LlmCalls = await ctx.RunLlmCalls.AsNoTracking().Where(x => x.RunId == runId).ToListAsync(cancellationToken);
        run.Sandboxes = await ctx.RunSandboxes.AsNoTracking().Where(x => x.RunId == runId).ToListAsync(cancellationToken);
        run.Decisions = await ctx.RunDecisions.AsNoTracking().Where(x => x.RunId == runId).ToListAsync(cancellationToken);
        return run;
    }
}
