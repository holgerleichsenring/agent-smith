using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Server.Services.Series;

/// <summary>2026-11-23-31dcd: what every run of a series' tickets cost, in one read. The sum is taken after the read:
/// SQLite cannot aggregate a decimal. 2026-11-25-9992a: per ticket, with its first start — the series' one state reads it.</summary>
public sealed class SeriesRunCosts(IUnitOfWork unitOfWork)
{
    public async Task<IReadOnlyDictionary<string, SeriesTicketSpend>> ByTicketAsync(
        string project, IReadOnlyCollection<string> ticketIds, CancellationToken ct) =>
        (await unitOfWork.Set<Run>().AsNoTracking().Where(r => r.Project == project && ticketIds.Contains(r.TicketId))
            .Select(r => new { r.TicketId, r.CostTotalUsd, r.StartedAt }).ToListAsync(ct))
            .GroupBy(r => r.TicketId, StringComparer.OrdinalIgnoreCase) // as the newest attempts are keyed
            .ToDictionary(g => g.Key, g => new SeriesTicketSpend(g.Sum(r => r.CostTotalUsd), g.Min(r => r.StartedAt)), StringComparer.OrdinalIgnoreCase);
}

/// <summary>2026-11-25-9992a: what every run of one ticket cost, and when the first of them started.</summary>
public sealed record SeriesTicketSpend(decimal CostUsd, DateTimeOffset FirstStartedAt);
