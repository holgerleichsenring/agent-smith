using AgentSmith.Contracts.Pocs;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;

namespace AgentSmith.Tests.Pocs;

/// <summary>2026-11-23-933da: PoC rows as the later slices' admission writes them.</summary>
internal static class PocRows
{
    public static Poc Poc(string name, string project, string target = "main", bool deleted = false)
    {
        return new Poc
        {
            Name = name, LiveName = deleted ? null : name, Project = project, Target = target,
            Folder = $"pocs/{project}/{name}", Owner = $"{project}-sub", OwnerName = project, State = deleted ? PocStates.Deleted : PocStates.Ready,
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
