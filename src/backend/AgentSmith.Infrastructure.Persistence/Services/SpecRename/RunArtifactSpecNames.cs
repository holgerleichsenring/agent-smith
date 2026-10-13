using AgentSmith.Infrastructure.Persistence.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Services.SpecRename;

/// <summary>
/// 2026-10-12-967ab: RunArtifacts kinds phase_record:/phase_review: become spec_record:/spec_review:.
/// A run whose spec was re-recorded across the rolling deploy may hold both; the spec-named row is
/// the one written later and the one every reader prefers, so the older one is removed.
/// </summary>
public sealed class RunArtifactSpecNames : IStoredSpecNameSurface
{
    public string Name => "RunArtifacts";

    public async Task<int> RewriteAsync(AgentSmithDbContext db, CancellationToken ct)
    {
        var changed = 0;
        foreach (var prefix in SpecRenameTable.ArtifactPrefixes)
            changed += await RenameAsync(db, prefix.Old, prefix.New, ct);
        return changed;
    }

    // Ids and kinds only: the content can be a whole spec record and is not touched.
    private static async Task<int> RenameAsync(AgentSmithDbContext db, string from, string to, CancellationToken ct)
    {
        var rows = await db.RunArtifacts.Where(a => a.Kind.StartsWith(from))
            .Select(a => new { a.Id, a.RunId, a.Kind }).ToListAsync(ct);
        foreach (var row in rows)
        {
            var kind = to + row.Kind[from.Length..];
            var artifact = db.RunArtifacts.Where(a => a.Id == row.Id);
            if (await db.RunArtifacts.AnyAsync(a => a.RunId == row.RunId && a.Kind == kind, ct))
                await artifact.ExecuteDeleteAsync(ct);
            else
                await artifact.ExecuteUpdateAsync(set => set.SetProperty(a => a.Kind, kind), ct);
        }
        return rows.Count;
    }
}
