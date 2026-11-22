using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// 2026-11-22-6d85c: the cancelled code rows a lost process ended that no newer code attempt of the same ticket follows —
/// the <see cref="DbStuckOnPreconditionReader"/> shape for the owed cancel reasons. A row with a Retry already stored on
/// it, or whose own cause is agent-smith's restart, is left out, so each interruption is restarted once. The finish time
/// is compared in memory: SQLite cannot compare a DateTimeOffset in SQL.
/// </summary>
public sealed class DbInterruptedAttemptReader(IServiceScopeFactory scopeFactory) : IInterruptedAttemptReader
{
    public async Task<IReadOnlyList<InterruptedAttempt>> ListAsync(DateTimeOffset since, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var runs = db.Set<Run>().AsNoTracking();
        var rows = await runs
            .Where(r => r.Pipeline == PipelinePresets.CodeName && r.Status == LeftInPlaceEnd.Cancelled && r.FinishedAt != null
                && (r.CancelReason == OwedWorkCancel.Interrupted || r.CancelReason == OwedWorkCancel.StaleLeaseReaped) && r.TicketId != ""
                && !runs.Any(n => n.Project == r.Project && n.TicketId == r.TicketId
                    && n.Pipeline == PipelinePresets.CodeName && n.Status != RunStatuses.Queued && string.Compare(n.Id, r.Id) > 0)
                && !db.Set<RunRetryRequest>().Any(q => q.RunId == r.Id)
                && !db.Set<RunAttempt>().Any(a => a.RunId == r.Id
                    && a.CauseChannel == AttemptChannels.Retry && a.CauseActor == AttemptChannels.RestartActor)
                && !db.Set<ClosedTicket>().Any(c => c.Project == r.Project && c.TicketId == r.TicketId)
                && !db.Set<LandedTicket>().Any(l => l.Project == r.Project && l.TicketId == r.TicketId))
            .OrderBy(r => r.Id)
            .Select(r => new { r.Project, r.TicketId, r.Id, r.FinishedAt })
            .ToListAsync(cancellationToken);
        return [.. rows.Where(r => r.FinishedAt >= since).Select(r => new InterruptedAttempt(r.Project, r.TicketId, r.Id))];
    }
}
