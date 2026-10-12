using AgentSmith.Contracts.Commands;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Services.SpecRename;

/// <summary>
/// 2026-10-12-967ab: RunSteps — the old command names (<see cref="CommandNames.Declared"/>, ":param"
/// kept), and the SpecId column filled from the old column on any row the migration's backfill did
/// not reach (a replica that still writes only the old column runs beside this one during the deploy).
/// </summary>
public sealed class RunStepSpecNames(RowPages pages) : IStoredSpecNameSurface
{
    public string Name => "RunSteps";

    public async Task<int> RewriteAsync(AgentSmithDbContext db, CancellationToken ct)
    {
        var backfilled = await db.RunSteps.Where(s => s.SpecId == null && s.PhaseId != null)
            .ExecuteUpdateAsync(set => set.SetProperty(s => s.SpecId, s => s.PhaseId), ct);
        return backfilled + await pages.RewriteAsync(db,
            db.RunSteps.Where(s => s.CommandName != null && s.CommandName.Contains("Phase")), s => s.Id, Rewrite, ct);
    }

    private static bool Rewrite(RunStep row)
    {
        var declared = CommandNames.Declared(row.CommandName!);
        if (declared == row.CommandName) return false;
        row.CommandName = declared;
        return true;
    }
}
