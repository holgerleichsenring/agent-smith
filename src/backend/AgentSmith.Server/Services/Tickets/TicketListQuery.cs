using AgentSmith.Contracts.Commands;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Services.Tickets;

/// <summary>
/// 2026-10-13-9a59a: the ticket list's one read — the newest code runs carrying a ticket, up to a
/// stated ceiling, light columns plus whether each ever started a step, and no JSON. Ordered by the
/// sortable run id: SQLite can neither ORDER BY nor compare a DateTimeOffset.
/// </summary>
public sealed class TicketListQuery(IServiceScopeFactory scopes)
{
    /// <summary>Every ticket an operator works in a quarter; the response says when it bit.</summary>
    public const int Ceiling = 5000;

    public async Task<TicketListRows> ReadAsync(ProjectScope project, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        // Run.Steps is an unmapped holder (RunChildConfiguration), so the EXISTS is spelled out.
        var steps = uow.Set<RunStep>();
        var runs = uow.Set<Run>().AsNoTracking()
            .Where(r => r.Pipeline == PipelinePresets.CodeName && r.TicketId != "").Within(project);
        var rows = await runs.OrderByDescending(r => r.Id).Take(Ceiling + 1)
            .Select(r => new TicketRunRow(new Run
            {
                Id = r.Id, Project = r.Project, Pipeline = r.Pipeline, TicketId = r.TicketId,
                TicketTitle = r.TicketTitle, Platform = r.Platform, Status = r.Status, StartedAt = r.StartedAt,
                FinishedAt = r.FinishedAt, ActsReadAt = r.ActsReadAt, CostTotalUsd = r.CostTotalUsd, CancelReason = r.CancelReason,
            }, steps.Any(s => s.RunId == r.Id)))
            .ToListAsync(ct);
        return new TicketListRows([.. rows.Take(Ceiling)], rows.Count > Ceiling);
    }
}
