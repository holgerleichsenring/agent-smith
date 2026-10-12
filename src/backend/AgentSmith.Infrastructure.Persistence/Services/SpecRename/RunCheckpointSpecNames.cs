using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;

namespace AgentSmith.Infrastructure.Persistence.Services.SpecRename;

/// <summary>2026-10-12-967ab: RunCheckpoints — the parked run's commands and context entries.</summary>
public sealed class RunCheckpointSpecNames(CheckpointJsonRewriter checkpoints, RowPages pages) : IStoredSpecNameSurface
{
    public string Name => "RunCheckpoints";

    public Task<int> RewriteAsync(AgentSmithDbContext db, CancellationToken ct) =>
        pages.RewriteAsync(db,
            db.RunCheckpoints.Where(c => c.RemainingCommandsJson.Contains("hase") || c.ContextJson.Contains("hase")),
            c => c.Id, Rewrite, ct);

    private bool Rewrite(RunCheckpoint row)
    {
        var commands = checkpoints.RewriteCommandsJson(row.RemainingCommandsJson);
        var context = checkpoints.RewriteContext(row.ContextJson);
        row.RemainingCommandsJson = commands ?? row.RemainingCommandsJson;
        row.ContextJson = context ?? row.ContextJson;
        return commands is not null || context is not null;
    }
}
