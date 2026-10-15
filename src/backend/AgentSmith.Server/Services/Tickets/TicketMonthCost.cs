using System.Globalization;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Services.Tickets;

/// <summary>
/// 2026-10-13-9a59a: what every run of the filtered project(s) cost this month. A run id begins
/// with its UTC start (yyyy-MM-ddTHH-mm-ss), so the month is a string prefix of the id — no
/// DateTimeOffset reaches SQL, and the decimals are summed in memory, which SQLite cannot do.
/// </summary>
public sealed class TicketMonthCost(IServiceScopeFactory scopes, TimeProvider clock)
{
    public async Task<decimal> OfAsync(ProjectScope project, CancellationToken ct)
    {
        var prefix = clock.GetUtcNow().UtcDateTime.ToString("yyyy-MM-", CultureInfo.InvariantCulture);
        using var scope = scopes.CreateScope();
        var runs = scope.ServiceProvider.GetRequiredService<IUnitOfWork>().Set<Run>().AsNoTracking()
            .Where(r => r.Id.StartsWith(prefix)).Within(project);
        var costs = await runs.Select(r => r.CostTotalUsd).ToListAsync(ct);
        return costs.Sum();
    }
}
