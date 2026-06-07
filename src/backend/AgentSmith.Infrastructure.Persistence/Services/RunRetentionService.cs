using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// Bounds the unbounded-growth tables. Prunes the raw RunEvent trail and the
/// non-final RunArtifact blobs older than the retention age; the Run + RunRepo +
/// the cost summary on Run are KEPT, so run history and totals survive while the
/// heavy per-event payloads and transient artifacts age out. The default age is a
/// deliberate decision (90 days), overridable by the caller.
/// </summary>
public sealed class RunRetentionService(
    IDbContextFactory<AgentSmithDbContext> contextFactory,
    TimeProvider timeProvider)
{
    public static readonly TimeSpan DefaultRetention = TimeSpan.FromDays(90);

    // Final artifacts the operator still reads after a run ends — never pruned.
    private static readonly string[] KeptArtifactKinds = ["result_md", "plan_md"];

    public async Task<int> PruneAsync(TimeSpan maxAge, CancellationToken cancellationToken)
    {
        await using var ctx = await contextFactory.CreateDbContextAsync(cancellationToken);
        var cutoff = timeProvider.GetUtcNow() - maxAge;

        // The DateTimeOffset comparison is filtered client-side: SQLite cannot
        // translate it in a query (Postgres/MySQL can), so one portable path
        // selects the doomed ids, then ExecuteDelete removes them set-based.
        var oldEventIds = (await ctx.RunEvents
                .Select(e => new { e.Id, e.Timestamp }).ToListAsync(cancellationToken))
            .Where(e => e.Timestamp < cutoff).Select(e => e.Id).ToList();
        var events = oldEventIds.Count == 0 ? 0 : await ctx.RunEvents
            .Where(e => oldEventIds.Contains(e.Id)).ExecuteDeleteAsync(cancellationToken);

        var oldArtifactIds = (await ctx.RunArtifacts
                .Where(a => !KeptArtifactKinds.Contains(a.Kind))
                .Select(a => new { a.Id, a.CreatedAt }).ToListAsync(cancellationToken))
            .Where(a => a.CreatedAt < cutoff).Select(a => a.Id).ToList();
        var artifacts = oldArtifactIds.Count == 0 ? 0 : await ctx.RunArtifacts
            .Where(a => oldArtifactIds.Contains(a.Id)).ExecuteDeleteAsync(cancellationToken);

        return events + artifacts;
    }
}
