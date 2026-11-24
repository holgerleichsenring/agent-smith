using AgentSmith.Contracts.Pocs;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;

namespace AgentSmith.Tests.Pocs;

/// <summary>2026-11-23-933da: PoC rows as the later slices' admission writes them. 2026-11-23-c81ed: a ready row carries
/// the desired state a deploy recorded (revision 1 with <see cref="Digest"/>), as a real ready PoC does — a ready row
/// without one is a pending delete.</summary>
internal static class PocRows
{
    public const string Digest = "sha256:1111111111111111111111111111111111111111111111111111111111111111";

    public static Poc Poc(string name, string project, string target = "main", bool deleted = false)
    {
        return new Poc
        {
            Name = name, LiveName = deleted ? null : name, Project = project, Target = target,
            Folder = $"pocs/{project}/{name}", Owner = $"{project}-sub", OwnerName = project, State = deleted ? PocStates.Deleted : PocStates.Ready,
            Revision = deleted ? null : 1, ImageDigest = deleted ? null : Digest,
        };
    }

    public static PocAuditEntry Entry(long pocId, string act, string? runId, long at = 1) =>
        new() { PocId = pocId, AtTicks = at, Actor = "ann", Act = act, RunId = runId };

    public static async Task<Poc> AddAsync(AgentSmithDbContext db, Poc poc)
    {
        db.Add(poc);
        await db.SaveChangesAsync();
        return poc;
    }
}
