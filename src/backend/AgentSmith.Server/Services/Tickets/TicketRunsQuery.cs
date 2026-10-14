using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Services.Tickets;

/// <summary>
/// 2026-10-13-9a59a: every run of one (project, ticket id) pair, every pipeline, ordered by run id,
/// each with whether it ever started a step. 2026-10-13-9a59e: lifted out of the detail reader, which
/// the series read shares it with.
/// </summary>
public sealed class TicketRunsQuery(IServiceScopeFactory scopes)
{
    public async Task<List<TicketRunRow>> ForTicketAsync(string project, string ticketId, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        // Run.Steps is an unmapped holder (RunChildConfiguration), so the EXISTS is spelled out.
        var steps = uow.Set<RunStep>();
        var rows = await uow.Set<Run>().AsNoTracking()
            .Where(r => r.Project == project && r.TicketId == ticketId)
            .OrderBy(r => r.Id)
            .Select(r => new { Run = r, HasStep = steps.Any(s => s.RunId == r.Id) })
            .ToListAsync(ct);
        return [.. rows.Select(r => new TicketRunRow(r.Run, r.HasStep))];
    }
}
