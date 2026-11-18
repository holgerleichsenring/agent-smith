using AgentSmith.Application.Services.Rework;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Services.Rework;

/// <summary>
/// 2026-11-17-3435c: after a refresh, a red CI on a pull request the newest green attempt left waiting asks for a fix: one
/// Sweep nudge, so the rework handler (which a Refresh nudge never reaches) weighs the red-CI act beside the ticket's
/// others. The record is marked acted on its head and verdict, so the next refresh asks nothing; the act itself is
/// idempotent by the attempt's cutoff. A fix that pushed nothing is the run end's nudge to find.
/// </summary>
public sealed class CiRedNudge(IServiceScopeFactory scopes, IPreviousAttemptReader attempts, IReworkNudges nudges)
{
    public async Task<bool> AfterRefreshAsync(ResolvedProject project, string ticketId, CancellationToken ct)
    {
        if (project.Merge.When != MergeWhen.OnCiGreen
            || await attempts.LatestAsync(project.Name, ticketId, null, ct) is not { } attempt) return false;
        using var scope = scopes.CreateScope();
        var records = await scope.ServiceProvider.GetRequiredService<PrReviewRecordRepository>().ByTicketAsync(project.Name, ticketId, ct);
        if (CiAct.Of(project, attempt, records) is not { FixPushedNothing: false } finding) return false;
        var fresh = records.Where(r => finding.Red.Any(p => p.Url == r.Url) && r.CiActed != r.Ci!.ActedMark).ToList();
        if (fresh.Count == 0) return false;
        var writer = scope.ServiceProvider.GetRequiredService<PrReviewRecordWriter>();
        foreach (var record in fresh) await writer.MarkCiActedAsync(record.PrKey, record.Ci!.ActedMark, ct);
        await nudges.EnqueueAsync(new ReworkNudgeRequest(project.Name, ticketId, ReworkNudgeOrigin.Sweep, fresh[0].Url), ct);
        return true;
    }
}
