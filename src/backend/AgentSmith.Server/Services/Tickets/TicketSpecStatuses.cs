using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Services.Tickets;

/// <summary>
/// 2026-10-13-9a59a: the RunSpec statuses of a set of runs, in one query — asked only for the
/// tickets on the page, never for every ticket the counts cover.
/// </summary>
public sealed class TicketSpecStatuses(IServiceScopeFactory scopes)
{
    public async Task<ILookup<string, string>> ForAsync(IReadOnlyList<string> runIds, CancellationToken ct)
    {
        if (runIds.Count == 0) return Array.Empty<(string, string)>().ToLookup(x => x.Item1, x => x.Item2);
        using var scope = scopes.CreateScope();
        var rows = await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().Set<RunSpec>().AsNoTracking()
            .Where(s => runIds.Contains(s.RunId))
            .Select(s => new { s.RunId, s.Status })
            .ToListAsync(ct);
        return rows.ToLookup(s => s.RunId, s => s.Status, StringComparer.Ordinal);
    }
}
