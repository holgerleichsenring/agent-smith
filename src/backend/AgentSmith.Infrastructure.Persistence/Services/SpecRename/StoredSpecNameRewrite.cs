using AgentSmith.Infrastructure.Persistence.Contracts;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Persistence.Services.SpecRename;

/// <summary>
/// 2026-10-12-967ab: rewrites every row written in the pre-rename names into the spec names, once,
/// after the schema is current. Idempotent and safe on several replicas at once: each surface
/// writes a value derived only from the row, so a second pass — or a second replica — finds
/// nothing left to change. Replicas still on 967aa read both shapes, so rows rewritten under them
/// stay readable.
/// </summary>
public sealed class StoredSpecNameRewrite(
    IEnumerable<IStoredSpecNameSurface> surfaces, ILogger<StoredSpecNameRewrite> logger)
{
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
