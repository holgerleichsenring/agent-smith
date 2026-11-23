using AgentSmith.Infrastructure.Persistence.Contracts;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Persistence.Services.SpecRename;

/// <summary>
/// 2026-10-12-967ab: rewrites every row written in the pre-rename names into the spec names, once,
/// after the schema is current. Idempotent and safe on several replicas at once: each surface
/// writes a value derived only from the row, so a second pass — or a second replica — finds
/// nothing left to change. Replicas still on 967aa read both shapes, so rows rewritten under them
/// stay readable.
/// <para>
/// 2026-11-22-747da: a data migration, recorded once it ran. It ran on every migrate since 967ab, so a store that had
/// already reached AddRunSpecIdColumns, the schema migration it shipped with, is recorded without running again.
/// </para>
/// </summary>
public sealed class StoredSpecNameRewrite(
    IEnumerable<IStoredSpecNameSurface> surfaces, ILogger<StoredSpecNameRewrite> logger) : IDataMigration
{
    public string Id => "2026-10-12-967ab-spec-names";

    /// <summary>AddRunSpecIdColumns in the shared set; the SQL Server twin is seconds later, PostgreSQL's set starts after.</summary>
    public string? ShippedWith => "20261012181922";

    public async Task<bool> RunAsync(AgentSmithDbContext db, CancellationToken ct)
    {
        await RewriteAsync(db, ct);
        return true;
    }

    /// <summary>Rewrites every surface; returns the rows changed per surface.</summary>
    public async Task<IReadOnlyDictionary<string, int>> RewriteAsync(AgentSmithDbContext db, CancellationToken ct)
    {
        var changed = new Dictionary<string, int>();
        foreach (var surface in surfaces)
        {
            changed[surface.Name] = await surface.RewriteAsync(db, ct);
            logger.LogInformation("Spec rename: {Rows} row(s) rewritten in {Surface}", changed[surface.Name], surface.Name);
        }
        return changed;
    }
}
