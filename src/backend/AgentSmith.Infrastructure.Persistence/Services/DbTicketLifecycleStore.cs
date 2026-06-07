using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// The system-of-record for a ticket's lifecycle status. The platform label is a
/// best-effort projection (p0246d) — this row is authoritative and wins on drift.
/// Upsert keyed by (Project, Platform, TicketId).
/// </summary>
public sealed class DbTicketLifecycleStore(IDbContextFactory<AgentSmithDbContext> contextFactory)
{
    public async Task<string?> GetStatusAsync(
        string project, string platform, TicketId ticketId, CancellationToken cancellationToken)
    {
        await using var ctx = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await ctx.TicketLifecycles.AsNoTracking()
            .Where(t => t.Project == project && t.Platform == platform && t.TicketId == ticketId.Value)
            .Select(t => t.Status)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task SetStatusAsync(
        string project, string platform, TicketId ticketId, string status, CancellationToken cancellationToken)
    {
        await using var ctx = await contextFactory.CreateDbContextAsync(cancellationToken);
        var row = await ctx.TicketLifecycles.FirstOrDefaultAsync(
            t => t.Project == project && t.Platform == platform && t.TicketId == ticketId.Value, cancellationToken);
        if (row is null)
        {
            ctx.TicketLifecycles.Add(new TicketLifecycle
            {
                Project = project, Platform = platform, TicketId = ticketId.Value, Status = status,
            });
        }
        else
        {
            row.Status = status;
        }
        await ctx.SaveChangesAsync(cancellationToken); // UpdatedAt stamped by the audit.
    }
}
