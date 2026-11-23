using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Persistence.Services.Repair;

/// <summary>
/// 2026-11-23-af60: from 2026-11-18-3863h until this spec the deploy record projection stored every record with
/// DeliveredAtTicks 0, so the follow judged each one pending too long. The delivery time is still in the run's own
/// DeployDelivered event; this fills every row that holds 0 from it. A row whose event is gone keeps 0, and a settled
/// row stays settled — only its shown delivery time changes.
/// </summary>
public sealed class DeployDeliveredAtBackfill(ILogger<DeployDeliveredAtBackfill> logger) : IDataMigration
{
    public string Id => "2026-11-23-af60-deploy-delivered-at";

    public string? ShippedWith => null;

    public async Task<bool> RunAsync(AgentSmithDbContext db, CancellationToken ct)
    {
        await RepairAsync(db, ct);
        return true;
    }

    private static readonly string Delivered = nameof(EventType.DeployDelivered);

    /// <summary>Fills the rows; returns how many were given their delivery time.</summary>
    public async Task<int> RepairAsync(AgentSmithDbContext db, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(db);
        var rows = await db.Set<DeployRecord>().Where(r => r.DeliveredAtTicks == 0).ToListAsync(ct);
        if (rows.Count == 0) return 0;
        var runIds = rows.Select(r => r.RunId).ToList();
        var times = (await db.RunEvents.AsNoTracking()
                .Where(e => runIds.Contains(e.RunId) && e.Type == Delivered)
                .Select(e => new { e.RunId, e.Seq, e.Timestamp }).ToListAsync(ct))
            .GroupBy(e => e.RunId)
            .ToDictionary(g => g.Key, g => g.OrderBy(e => e.Seq).First().Timestamp, StringComparer.Ordinal);
        var repaired = 0;
        foreach (var row in rows)
        {
            if (!times.TryGetValue(row.RunId, out var at)) continue;
            row.DeliveredAtTicks = at.UtcTicks;
            repaired++;
        }
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Deploy delivered-at backfill: {Rows} of {Total} deploy record(s) given their delivery time",
            repaired, rows.Count);
        return repaired;
    }
}
