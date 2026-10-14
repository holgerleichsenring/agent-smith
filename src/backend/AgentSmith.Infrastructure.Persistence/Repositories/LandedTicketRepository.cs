using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// 2026-10-14-dbceg: the landed tickets, served to singleton callers by opening a scope per operation.
/// Insert only: a stored key answers false, and so does a racing replica's unique violation — its
/// entry is detached, the winner's row stands. No update and no delete.
/// </summary>
public sealed class LandedTicketRepository(IServiceScopeFactory scopeFactory) : ITicketLandingStore
{
    public async Task<bool> TryRecordAsync(TicketLandingFact landing, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(landing);
        using var scope = scopeFactory.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        if (await unitOfWork.Set<LandedTicket>().AsNoTracking()
                .AnyAsync(l => l.Tracker == landing.Tracker && l.TicketKey == landing.TicketKey, cancellationToken))
            return false;
        var entry = unitOfWork.Add(Row(landing));
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException ex) when (scope.ServiceProvider.GetRequiredService<IUniqueViolationTranslator>().IsUniqueViolation(ex))
        {
            entry.State = EntityState.Detached;
            return false;
        }
    }

    public async Task<IReadOnlyList<TicketLandingFact>> LandedAsync(
        string tracker, IReadOnlyCollection<string> ticketKeys, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ticketKeys);
        if (ticketKeys.Count == 0) return [];
        var keys = ticketKeys.Distinct(StringComparer.Ordinal).ToList();
        using var scope = scopeFactory.CreateScope();
        var rows = await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().Set<LandedTicket>().AsNoTracking()
            .Where(l => l.Tracker == tracker && keys.Contains(l.TicketKey)).OrderBy(l => l.Id).ToListAsync(cancellationToken);
        return [.. rows.Select(Fact)];
    }

    // SQLite cannot compare a DateTimeOffset: newest landing first (Id is insert order), stopping at the window.
    public async Task<IReadOnlyList<TicketRequirementEdge>> DependentsOfLandingsSinceAsync(
        DateTimeOffset since, IReadOnlyCollection<string> projects, int take, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var found = new List<TicketRequirement>();
        await foreach (var row in db.Set<TicketRequirement>().AsNoTracking().Where(r => r.StartsWhenMet && projects.Contains(r.Project)
                && !db.Set<LandedTicket>().Any(own => own.Tracker == r.Tracker && own.TicketKey == r.TicketKey))
            .Join(db.Set<LandedTicket>(), r => new { r.Tracker, Key = r.RequiredTicketKey }, l => new { l.Tracker, Key = l.TicketKey },
                (r, l) => new { Edge = r, l.Id, l.CreatedAt }).OrderByDescending(x => x.Id).AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            if (row.CreatedAt < since || found.Count >= take) break;
            if (!found.Exists(f => f.Tracker == row.Edge.Tracker && f.TicketKey == row.Edge.TicketKey)) found.Add(row.Edge);
        }
        return [.. found.Select(TicketRequirementRepository.Edge)];
    }

    private static LandedTicket Row(TicketLandingFact f) => new()
    {
        Tracker = f.Tracker, TicketKey = f.TicketKey, TicketId = f.TicketId, Project = f.Project, Kind = f.Kind,
        LandedAt = f.LandedAt, By = f.By, Note = f.Note, PrUrl = f.PrUrl,
    };

    private static TicketLandingFact Fact(LandedTicket l) =>
        new(l.Tracker, l.TicketKey, l.TicketId, l.Project, l.Kind, l.LandedAt, l.By, l.Note, l.PrUrl);
}
