using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Server.Services.Series;

/// <summary>2026-11-23-31dcd: what every run of a series' tickets cost, in one read. The sum is taken after the read:
/// SQLite cannot aggregate a decimal.</summary>
public sealed class SeriesRunCosts(IUnitOfWork unitOfWork)
{
    public async Task<decimal> OfAsync(string project, IReadOnlyCollection<string> ticketIds, CancellationToken ct) =>
        (await unitOfWork.Set<Run>().AsNoTracking().Where(r => r.Project == project && ticketIds.Contains(r.TicketId))
            .Select(r => r.CostTotalUsd).ToListAsync(ct)).Sum();
}
