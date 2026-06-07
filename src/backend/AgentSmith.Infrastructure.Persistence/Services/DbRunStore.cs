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
        return await ctx.Runs.AsNoTracking()
            .Include(r => r.Repos).Include(r => r.Steps).Include(r => r.LlmCalls)
            .Include(r => r.Sandboxes).Include(r => r.Decisions)
            .FirstOrDefaultAsync(r => r.Id == runId, cancellationToken);
    }
}
