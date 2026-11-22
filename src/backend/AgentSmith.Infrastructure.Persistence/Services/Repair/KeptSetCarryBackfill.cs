using System.Text.Json;
using System.Text.Json.Nodes;
using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Persistence.Services.Repair;

/// <summary>
/// 2026-11-22-a720a: before this spec an attempt that kept its spec set unchanged published no revision, and the attempt
/// projection dropped the executed ids it carried with it — so a series completed by Retry unchanged read as unfinished
/// and started another attempt. The ids are still in the attempt's own SpecSetPublished events; this fills every attempt
/// row that has none from them, in C# so every provider runs the same code. A row whose events carry none gets an empty
/// list, which reads exactly as before, so a second pass finds nothing left to look at.
/// <para>2026-11-22-747da: a data migration, run once and recorded; no schema migration shipped with it.</para>
/// </summary>
public sealed class KeptSetCarryBackfill(ILogger<KeptSetCarryBackfill> logger) : IDataMigration
{
    public string Id => "2026-11-22-a720a-kept-set-carry";

    public string? ShippedWith => null;

    public async Task<bool> RunAsync(AgentSmithDbContext db, CancellationToken ct)
    {
        await RepairAsync(db, ct);
        return true;
    }

    /// <summary>Attempt rows per page.</summary>
    public const int PageSize = 200; // not a limit: every page is drained, the size only bounds memory per round trip

    private static readonly string Published = nameof(EventType.SpecSetPublished);

    /// <summary>Fills the rows; returns how many were given carried ids.</summary>
    public async Task<int> RepairAsync(AgentSmithDbContext db, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(db);
        var repaired = 0;
        while (await PageAsync(db, ct) is { Count: > 0 } page)
        {
            var events = await EventsAsync(db, [.. page.Select(a => a.RunId)], ct);
            foreach (var attempt in page)
                repaired += Fill(attempt, events.GetValueOrDefault(attempt.RunId) ?? []) ? 1 : 0;
            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();
        }
        logger.LogInformation("Kept-set carry backfill: {Rows} attempt row(s) given their carried spec ids", repaired);
        return repaired;
    }

    private static Task<List<RunAttempt>> PageAsync(AgentSmithDbContext db, CancellationToken ct) =>
        db.Set<RunAttempt>()
            .Where(a => a.CarriedSpecIdsJson == null && db.RunEvents.Any(e => e.RunId == a.RunId && e.Type == Published))
            .OrderBy(a => a.Id).Take(PageSize).ToListAsync(ct);

    private static async Task<Dictionary<string, List<string?>>> EventsAsync(
        AgentSmithDbContext db, List<string> runIds, CancellationToken ct) =>
        (await db.RunEvents.AsNoTracking()
            .Where(e => runIds.Contains(e.RunId) && e.Type == Published)
            .OrderBy(e => e.Seq).Select(e => new { e.RunId, e.PayloadJson }).ToListAsync(ct))
        .GroupBy(e => e.RunId).ToDictionary(g => g.Key, g => g.Select(e => e.PayloadJson).ToList(), StringComparer.Ordinal);

    // The first publish with specs is the one the projection now records; with none, the first that names carried ids.
    private bool Fill(RunAttempt attempt, List<string?> payloads)
    {
        var parsed = payloads.Select(Parse).OfType<JsonObject>().ToList();
        var source = parsed.FirstOrDefault(p => p["Specs"] is JsonArray { Count: > 0 })
            ?? parsed.FirstOrDefault(p => p["CarriedSpecIds"] is JsonArray);
        var carried = source?["CarriedSpecIds"] is JsonArray ids
            ? ids.Select(Text).OfType<string>().ToList() : [];
        attempt.CarriedSpecIdsJson = JsonSerializer.Serialize(carried);
        attempt.SeriesId ??= Text(source?["SeriesId"]);
        return carried.Count > 0;
    }

    private static string? Text(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private JsonObject? Parse(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload)) return null;
        try { return JsonNode.Parse(payload) as JsonObject; }
        catch (JsonException ex)
        {
            logger.LogDebug(ex, "A SpecSetPublished payload could not be read; it names no carried ids");
            return null;
        }
    }
}
