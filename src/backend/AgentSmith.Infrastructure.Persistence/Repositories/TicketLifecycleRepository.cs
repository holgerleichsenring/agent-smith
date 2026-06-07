using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// The system-of-record for a ticket's lifecycle status, over a SCOPED unit of
/// work. The platform label is a best-effort projection (p0246d) — this row is
/// authoritative and wins on drift. Upsert keyed by (Project, Platform, TicketId).
/// </summary>
public sealed class TicketLifecycleRepository(IUnitOfWork unitOfWork)
{
    public Task<string?> GetStatusAsync(string project, string platform, TicketId ticketId, CancellationToken ct) =>
        unitOfWork.Set<TicketLifecycle>().AsNoTracking()
            .Where(t => t.Project == project && t.Platform == platform && t.TicketId == ticketId.Value)
            .Select(t => t.Status)
            .FirstOrDefaultAsync(ct);

    public async Task SetStatusAsync(
        string project, string platform, TicketId ticketId, string status, CancellationToken ct)
    {
        var row = await unitOfWork.Set<TicketLifecycle>().FirstOrDefaultAsync(
            t => t.Project == project && t.Platform == platform && t.TicketId == ticketId.Value, ct);
        if (row is null)
            unitOfWork.Add(new TicketLifecycle
            {
                Project = project, Platform = platform, TicketId = ticketId.Value, Status = status,
            });
        else
            row.Status = status;
        await unitOfWork.SaveChangesAsync(ct); // UpdatedAt stamped by the audit.
    }
}
