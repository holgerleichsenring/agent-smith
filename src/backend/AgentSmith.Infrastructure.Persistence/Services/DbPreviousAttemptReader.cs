using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// 2026-10-08-7c0e: the newest code-pipeline Run row of a ticket, ordered by id (run ids are
/// time-sortable and SQLite cannot ORDER BY a DateTimeOffset). A queued row is a capacity
/// reservation, never an attempt; a scan is not an attempt at the ticket's work.
/// </summary>
public sealed class DbPreviousAttemptReader(IServiceScopeFactory scopeFactory) : IPreviousAttemptReader
{
    public async Task<PreviousAttempt?> LatestAsync(
        string project, string ticketId, string? excludingRunId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(project) || string.IsNullOrWhiteSpace(ticketId)) return null;
        using var scope = scopeFactory.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var run = await uow.Set<Run>().AsNoTracking()
            .Where(r => r.Project == project && r.TicketId == ticketId
                && r.Pipeline == PipelinePresets.CodeName && r.Status != RunStatuses.Queued
                && (excludingRunId == null || r.Id != excludingRunId))
            .OrderByDescending(r => r.Id)
            .Select(r => new { r.Id, r.Status, r.StartedAt, r.FinishedAt })
            .FirstOrDefaultAsync(cancellationToken);
        return run is null ? null
            : new PreviousAttempt(run.Id, run.Status, run.StartedAt, run.FinishedAt is not null);
    }
}
