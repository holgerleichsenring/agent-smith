using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// The DB-backed single-run lease. TryClaim is an INSERT the
/// UNIQUE(Project,TicketId) index rejects for a duplicate — the provider-native
/// violation is mapped to AlreadyClaimed via <see cref="IUniqueViolationTranslator"/>,
/// never a blanket catch. Uses a context factory so the (singleton) claim path
/// gets a fresh, non-shared DbContext per operation.
/// </summary>
public sealed class DbActiveRunLease(
    IDbContextFactory<AgentSmithDbContext> contextFactory,
    IUniqueViolationTranslator violationTranslator,
    TimeProvider timeProvider) : IActiveRunLease
{
    public async Task<LeaseClaimOutcome> TryClaimAsync(
        string project, TicketId ticketId, CancellationToken cancellationToken)
    {
        await using var ctx = await contextFactory.CreateDbContextAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        ctx.ActiveRuns.Add(new ActiveRun
        {
            Project = project, TicketId = ticketId.Value, ClaimedAt = now, HeartbeatAt = now,
        });
        try
        {
            await ctx.SaveChangesAsync(cancellationToken);
            return LeaseClaimOutcome.Claimed;
        }
        catch (DbUpdateException ex) when (violationTranslator.IsUniqueViolation(ex))
        {
            return LeaseClaimOutcome.AlreadyClaimed;
        }
        catch (DbUpdateException)
        {
            return LeaseClaimOutcome.Error;
        }
    }

    public async Task ReleaseAsync(string project, TicketId ticketId, CancellationToken cancellationToken)
    {
        await using var ctx = await contextFactory.CreateDbContextAsync(cancellationToken);
        await ctx.ActiveRuns
            .Where(a => a.Project == project && a.TicketId == ticketId.Value)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task AttachRunAsync(
        string project, TicketId ticketId, string runId, string? jobId, CancellationToken cancellationToken)
    {
        await using var ctx = await contextFactory.CreateDbContextAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        await ctx.ActiveRuns
            .Where(a => a.Project == project && a.TicketId == ticketId.Value)
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.RunId, runId)
                .SetProperty(a => a.JobId, jobId)
                .SetProperty(a => a.HeartbeatAt, now), cancellationToken);
    }

    public async Task RenewHeartbeatAsync(string project, TicketId ticketId, CancellationToken cancellationToken)
    {
        await using var ctx = await contextFactory.CreateDbContextAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        await ctx.ActiveRuns
            .Where(a => a.Project == project && a.TicketId == ticketId.Value)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.HeartbeatAt, now), cancellationToken);
    }

    public async Task<IReadOnlyList<StaleLease>> FindStaleAsync(
        TimeSpan olderThan, CancellationToken cancellationToken)
    {
        await using var ctx = await contextFactory.CreateDbContextAsync(cancellationToken);
        var cutoff = timeProvider.GetUtcNow() - olderThan;
        // The active-lease table holds at most one row per in-flight ticket, so
        // loading it and filtering the heartbeat in memory is cheap — and it is
        // provider-portable: SQLite cannot translate a DateTimeOffset comparison
        // in a query (Postgres/MySQL can, but one code path beats per-provider SQL).
        var rows = await ctx.ActiveRuns
            .Select(a => new { a.Project, a.TicketId, a.RunId, a.JobId, a.HeartbeatAt })
            .ToListAsync(cancellationToken);
        return rows
            .Where(r => r.HeartbeatAt < cutoff)
            .Select(r => new StaleLease(r.Project, new TicketId(r.TicketId), r.RunId, r.JobId))
            .ToList();
    }
}
