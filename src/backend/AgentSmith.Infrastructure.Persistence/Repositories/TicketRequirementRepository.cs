using System.Linq.Expressions;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// 2026-10-14-dbcef: the ticket requirements, served to singleton callers by opening a scope per
/// operation. Insert only and idempotent: a stored edge is skipped. A racing writer's unique violation
/// rolls this call's whole save back, so the entries are detached, the dependent's edges re-read and
/// the edges still missing inserted once more; a second violation is a real fault and propagates.
/// </summary>
public sealed class TicketRequirementRepository(IServiceScopeFactory scopeFactory) : ITicketRequirementStore
{
    public async Task<int> AddAsync(IReadOnlyList<TicketRequirementEdge> edges, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(edges);
        if (edges.Count == 0) return 0;
        Validate(edges);
        using var scope = scopeFactory.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var violations = scope.ServiceProvider.GetRequiredService<IUniqueViolationTranslator>();
        var staged = await StageMissingAsync(unitOfWork, edges, cancellationToken);
        if (staged.Count == 0) return 0;
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return staged.Count;
        }
        catch (DbUpdateException ex) when (violations.IsUniqueViolation(ex))
        {
            foreach (var entry in staged) entry.State = EntityState.Detached;
        }
        var retried = await StageMissingAsync(unitOfWork, edges, cancellationToken);
        if (retried.Count > 0) await unitOfWork.SaveChangesAsync(cancellationToken);
        return retried.Count;
    }

    public Task<IReadOnlyList<TicketRequirementEdge>> RequiredOfAsync(
        string tracker, string ticketKey, CancellationToken cancellationToken) =>
        ReadAsync(r => r.Tracker == tracker && r.TicketKey == ticketKey, cancellationToken);

    public Task<IReadOnlyList<TicketRequirementEdge>> DependentsOfAsync(
        string tracker, string requiredTicketKey, CancellationToken cancellationToken) =>
        ReadAsync(r => r.Tracker == tracker && r.RequiredTicketKey == requiredTicketKey, cancellationToken);

    public Task<IReadOnlyList<TicketRequirementEdge>> ByProjectAsync(string project, CancellationToken cancellationToken) =>
        ReadAsync(r => r.Project == project, cancellationToken);

    private static void Validate(IReadOnlyList<TicketRequirementEdge> edges)
    {
        var first = edges[0];
        if (edges.Any(e => e.Tracker != first.Tracker || e.TicketKey != first.TicketKey))
            throw new ArgumentException("One call stores the requirements of one dependent ticket.", nameof(edges));
        if (edges.Any(e => e.RequiredTicketKey == e.TicketKey))
            throw new ArgumentException($"Ticket {first.TicketKey} cannot require itself.", nameof(edges));
        if (edges.Any(e => e.StartsWhenMet != first.StartsWhenMet))
            throw new ArgumentException(
                $"The requirements of ticket {first.TicketKey} disagree on whether it starts when they are met.", nameof(edges));
    }

    private static async Task<List<EntityEntry>> StageMissingAsync(
        IUnitOfWork unitOfWork, IReadOnlyList<TicketRequirementEdge> edges, CancellationToken ct)
    {
        var first = edges[0];
        var stored = await unitOfWork.Set<TicketRequirement>().AsNoTracking()
            .Where(r => r.Tracker == first.Tracker && r.TicketKey == first.TicketKey)
            .Select(r => r.RequiredTicketKey).ToListAsync(ct);
        var present = stored.ToHashSet(StringComparer.Ordinal);
        return [.. edges.Where(e => present.Add(e.RequiredTicketKey)).Select(e => unitOfWork.Add(Row(e)))];
    }

    private async Task<IReadOnlyList<TicketRequirementEdge>> ReadAsync(
        Expression<Func<TicketRequirement, bool>> filter, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var rows = await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().Set<TicketRequirement>()
            .AsNoTracking().Where(filter).OrderBy(r => r.Id).ToListAsync(ct);
        return [.. rows.Select(Edge)];
    }

    private static TicketRequirement Row(TicketRequirementEdge e) => new()
    {
        Tracker = e.Tracker, TicketKey = e.TicketKey, TicketId = e.TicketId, Project = e.Project,
        RequiredTicketKey = e.RequiredTicketKey, RequiredTicketId = e.RequiredTicketId,
        StartsWhenMet = e.StartsWhenMet, FiledInConversation = e.FiledInConversation,
    };

    internal static TicketRequirementEdge Edge(TicketRequirement r) => new(
        r.Tracker, r.TicketKey, r.TicketId, r.Project, r.RequiredTicketKey, r.RequiredTicketId,
        r.StartsWhenMet, r.CreatedAt, r.FiledInConversation);
}
