using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Tests.Persistence.SpecRename;

/// <summary>2026-10-12-967ab: every rewritten column as text, in a stable order, to compare two passes.</summary>
internal static class StoreText
{
    public static async Task<List<string?>> DumpAsync(AgentSmithDbContext db)
    {
        var run = await db.Runs.AsNoTracking().SingleAsync();
        var checkpoint = await db.RunCheckpoints.AsNoTracking().SingleAsync();
        var session = await db.SpecDialogSessions.AsNoTracking().SingleAsync();
        var step = await db.RunSteps.AsNoTracking().SingleAsync();
        return
        [
            run.PlannedStepsJson, run.QueuedRequestJson, run.AcceptanceJson,
            checkpoint.RemainingCommandsJson, checkpoint.ContextJson,
            (await db.QueuedTickets.AsNoTracking().SingleAsync()).InitialContextJson,
            (await db.ApprovedSeries.AsNoTracking().SingleAsync()).ContentJson,
            session.TranscriptJson, session.LatestFilingJson,
            .. await db.Set<SpecDialogProposal>().AsNoTracking().OrderBy(p => p.Id).Select(p => p.ProposalJson).ToListAsync(),
            step.CommandName, step.SpecId, (await db.RunDecisions.AsNoTracking().SingleAsync()).SpecId,
            .. await db.RunEvents.AsNoTracking().OrderBy(e => e.Seq).Select(e => e.Type + e.PayloadJson).ToListAsync(),
            .. await db.RunArtifacts.AsNoTracking().OrderBy(a => a.Id).Select(a => a.Kind + a.Content).ToListAsync(),
        ];
    }
}
