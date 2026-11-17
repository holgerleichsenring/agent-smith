using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// 2026-11-16-f3fbc: the failed code rows a precondition ended that no newer code attempt of the same ticket
/// follows — the row <see cref="DbPreviousAttemptReader"/> reads as StoppedOnPrecondition. Queued reservations
/// are not attempts, so a release waiting for capacity leaves the ticket listed; its handler finds it queued.
/// A ticket a person closed here or that landed is left out, matched by project and ticket as the close mark and
/// the landing store them, so the list does not grow with every ticket that ever stopped; the release's own
/// TicketStartHold still answers by tracker key.
/// </summary>
public sealed class DbStuckOnPreconditionReader(IServiceScopeFactory scopeFactory) : IStuckOnPreconditionReader
{
    private static readonly string[] Kinds = [.. RunEndKinds.Preconditions];

    public async Task<IReadOnlyList<StuckOnPrecondition>> ListAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var runs = db.Set<Run>().AsNoTracking();
        var rows = await runs
            .Where(r => r.Pipeline == PipelinePresets.CodeName && r.Status == RunStatuses.Failed
                && r.FinishedAt != null && r.EndKind != null && Kinds.Contains(r.EndKind) && r.TicketId != ""
                && !runs.Any(n => n.Project == r.Project && n.TicketId == r.TicketId
                    && n.Pipeline == PipelinePresets.CodeName && n.Status != RunStatuses.Queued
                    && string.Compare(n.Id, r.Id) > 0)
                && !db.Set<ClosedTicket>().Any(c => c.Project == r.Project && c.TicketId == r.TicketId)
                && !db.Set<LandedTicket>().Any(l => l.Project == r.Project && l.TicketId == r.TicketId))
            .OrderBy(r => r.Id)
            .Select(r => new { r.Project, r.TicketId, r.Id, r.EndKind, r.EndDetailJson, r.FinishedAt })
            .ToListAsync(cancellationToken);
        return [.. rows.Select(r => new StuckOnPrecondition(
            r.Project, r.TicketId, r.Id, r.EndKind!, EndDetailJson.Read(r.EndDetailJson), r.FinishedAt!.Value))];
    }
}
