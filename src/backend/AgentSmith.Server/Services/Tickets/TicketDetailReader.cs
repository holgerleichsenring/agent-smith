using AgentSmith.Application.Services;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;

namespace AgentSmith.Server.Services.Tickets;

/// <summary>
/// 2026-10-13-9a59a: one ticket — every run of the (project, ticket id) pair, every pipeline and no
/// cap. The attempts are numbered from 1 by run id and composed by the filed-work compose half, so
/// this page and the conversation's filed-work panel cannot disagree; everything else is another run.
/// </summary>
public sealed class TicketDetailReader(
    TicketRunsQuery runQuery,
    TicketAttempts attempts,
    TicketAttemptMarker marker,
    TicketLiveStateReader live,
    TicketSummaryComposer composer,
    FiledWorkRunsReader composed,
    TicketPullRequestReader pullRequests,
    TicketAttemptRecords attemptRecords)
{
    /// <summary>Null when the pair has no code run — it is not a ticket.</summary>
    public async Task<TicketDetailView?> ReadAsync(string project, string ticketId, CancellationToken ct)
    {
        var rows = await runQuery.ForTicketAsync(project, ticketId, ct);
        var code = rows.Where(r => r.Run.Pipeline == PipelinePresets.CodeName).ToList();
        if (code.Count == 0) return null;
        var ordered = attempts.Of(code);
        var state = await live.ReadAsync([attempts.Head(code).Run], ct);
        var runs = await composed.ForRunsAsync([.. ordered.Select(a => a.Run)], ct);
        var specs = runs.SelectMany(r => r.Specs.Select(s => (r.RunId, s.Status)))
            .ToLookup(s => s.RunId, s => s.Status, StringComparer.Ordinal);
        var records = await attemptRecords.ForAsync([.. ordered.Select(a => a.Run.Id)], ct);
        return new TicketDetailView(
            composer.Compose(code, state, specs, await pullRequests.TicketAsync(project, ticketId, ct), records),
            TicketBranchNamer.Compose(new TicketId(ticketId)).Value,
            [.. ordered.Select((a, i) => Attempt(i + 1, a.Run, runs[i], marker.Mark(a.Run, state, specs),
                records.GetValueOrDefault(a.Run.Id)))],
            [.. rows.Where(r => !attempts.IsAttempt(r)).Select(Other)]);
    }

    private static TicketAttemptView Attempt(
        int number, Run run, FiledWorkRunView view, TicketAttemptMark mark, TicketAttemptRecord? record) =>
        new(number, run.Id, run.Status, run.StartedAt, run.FinishedAt, run.CostTotalUsd,
            mark.EndedOnInfrastructure, mark.HandedBack, view.PullRequests, view.Specs, view.PendingQuestion,
            record?.Cause, record?.Revision);

    private static TicketOtherRunView Other(TicketRunRow row) =>
        new(row.Run.Id, row.Run.Pipeline, row.Run.Status, row.Run.StartedAt, row.Run.FinishedAt,
            row.Run.CostTotalUsd, NeverStarted: row.Run.Pipeline == PipelinePresets.CodeName);
}
