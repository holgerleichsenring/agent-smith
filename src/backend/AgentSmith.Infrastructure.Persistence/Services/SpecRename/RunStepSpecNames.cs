using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;

namespace AgentSmith.Infrastructure.Persistence.Services.SpecRename;

/// <summary>
/// 2026-10-12-967ab: RunSteps — the old command names (<see cref="SpecRenameTable.CommandName"/>,
/// ":param" kept). The spec id column is the migration's business (DropRunPhaseIdColumns).
/// </summary>
public sealed class RunStepSpecNames(RowPages pages) : IStoredSpecNameSurface
{
    public string Name => "RunSteps";

    public Task<int> RewriteAsync(AgentSmithDbContext db, CancellationToken ct) =>
        pages.RewriteAsync(db,
            db.RunSteps.Where(s => s.CommandName != null && s.CommandName.Contains("Phase")), s => s.Id, Rewrite, ct);

    private static bool Rewrite(RunStep row)
    {
        if (SpecRenameTable.CommandName(row.CommandName!) is not { } current) return false;
        row.CommandName = current;
        return true;
    }
}
