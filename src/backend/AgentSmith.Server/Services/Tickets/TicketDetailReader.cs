using AgentSmith.Application.Services;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;

namespace AgentSmith.Server.Services.Tickets;

/// <summary>
/// 2026-10-13-9a59a: one ticket — every run of the (project, ticket id) pair, every pipeline and no
/// cap. The attempts are numbered from 1 by run id and composed by the filed-work compose half, so
/// this page and the conversation's filed-work panel cannot disagree; everything else is another run.
/// <para>2026-10-13-9a59h: the detail says what was acted since a failed last attempt.</para>
/// <para>2026-10-26-bdd0f: and its spec derivations, a filed never-run ticket's included.</para>
/// <para>2026-10-26-af69: the summary's cost counts them, as the list row does.</para>
/// <para>2026-11-23-1e76a: a failed last attempt is offered to the between-attempts check's kick queue, as the list
/// offers it, and never checked here — the page waits on no tracker; a changed check nudges the ticket's views. The
/// reads after the run query depend on it alone and run side by side, each store in a scope of its own.</para>
/// </summary>
public sealed class TicketDetailReader(
    TicketRunsQuery runQuery,
    TicketAttempts attempts,
    TicketAttemptMarker marker,
    TicketLiveStateReader live,
    TicketSummaryComposer composer,
    FiledWorkRunsReader composed,
    TicketPullRequestReader pullRequests,
    TicketAttemptRecords attemptRecords,
    Checks.TicketCheckKicks kicks,
    TicketStandings standings,
    Checks.TicketStuckRule stuck,
    TicketLaunchEnds launchEnds,
    FiledTicketRows filed,
    TicketDerivations derivations) // 2026-10-26-bdd0f
{
    /// <summary>2026-10-14-dbcen: with no code run, the pair's never-run row with no attempts; null when neither exists.</summary>
    public async Task<TicketDetailView?> ReadAsync(string project, string ticketId, CancellationToken ct)
    {
        var rows = await runQuery.ForTicketAsync(project, ticketId, ct);
        var code = rows.Where(r => r.Run.Pipeline == PipelinePresets.CodeName).ToList();
        var derived = derivations.OfAsync(rows.Select(r => r.Run), ct);
        if (code.Count == 0) return await NeverRunAsync(project, ticketId, derived, ct);
        var ordered = attempts.Of(code);
        var head = attempts.Head(code).Run;
        if (head.Status == RunStatuses.Failed) kicks.Offer([new Checks.TicketCheckKick(project, ticketId)]);
        var read = await JoinAsync(project, ticketId, ordered, head, derived, ct);
        var specs = read.Runs.SelectMany(r => r.Specs.Select(s => (r.RunId, s.Status)))
            .ToLookup(s => s.RunId, s => s.Status, StringComparer.Ordinal);
        return new TicketDetailView(
            composer.Compose(code, read.State, specs, read.PrViews, read.Records, read.Standing, read.Launches, read.Derived.Sum(d => d.Cost)),
            Branch(ticketId),
            [.. ordered.Select((a, i) => Attempt(i + 1, a.Run, read.Runs[i], marker.Mark(a.Run, read.State, specs, read.Launches),
                read.Records.GetValueOrDefault(a.Run.Id)))],
            [.. rows.Where(r => !attempts.IsAttempt(r)).Select(Other)],
            stuck.ActedSince(head, read.Standing.CheckOf(head.Id), read.PrRecords), read.Derived);
    }

    private async Task<TicketDetailView?> NeverRunAsync(
        string project, string ticketId, Task<IReadOnlyList<TicketDerivationView>> derived, CancellationToken ct)
    {
        var row = filed.OneAsync(project, ticketId, ct);
        await Task.WhenAll(derived, row);
        return await row is { } neverRun
            ? new TicketDetailView(neverRun with { Cost = (await derived).Sum(d => d.Cost) }, Branch(ticketId), [], [], Derivations: await derived)
            : null;
    }

    // Every read here needs only the run rows; none waits on another.
    private async Task<Joined> JoinAsync(string project, string ticketId, IReadOnlyList<TicketRunRow> ordered, Run head,
        Task<IReadOnlyList<TicketDerivationView>> derived, CancellationToken ct)
    {
        var ids = ordered.Select(a => a.Run.Id).ToList();
        var state = live.ReadAsync([head], ct);
        var standing = standings.ReadAsync([head], ct);
        var prs = pullRequests.TicketRecordsAsync(project, ticketId, ct);
        var runs = composed.ForRunsAsync([.. ordered.Select(a => a.Run)], ct);
        var records = attemptRecords.ForAsync(ids, ct);
        var launches = launchEnds.ForAsync(ids, ct);
        await Task.WhenAll(derived, state, standing, prs, runs, records, launches);
        var (prRecords, prViews) = await prs;
        return new Joined(await derived, await state, await standing, prRecords, prViews, await runs, await records, await launches);
    }

    private static string Branch(string ticketId) => TicketBranchNamer.Compose(new TicketId(ticketId)).Value;

    private sealed record Joined(
        IReadOnlyList<TicketDerivationView> Derived, TicketLiveState State, TicketStanding Standing,
        IReadOnlyList<PrReviewRecordView> PrRecords, IReadOnlyList<TicketPullRequestView> PrViews,
        IReadOnlyList<FiledWorkRunView> Runs, IReadOnlyDictionary<string, TicketAttemptRecord> Records,
        IReadOnlyDictionary<string, RunLastLaunch> Launches);

    private static TicketAttemptView Attempt(
        int number, Run run, FiledWorkRunView view, TicketAttemptMark mark, TicketAttemptRecord? record) =>
        new(number, run.Id, run.Status, run.StartedAt, run.FinishedAt, run.CostTotalUsd,
            mark.EndedOnInfrastructure, mark.HandedBack, view.PullRequests, view.Specs, view.PendingQuestion,
            record?.Cause, record?.Revision, run.Summary, run.EndKind, EndDetailJson.Read(run.EndDetailJson));

    private static TicketOtherRunView Other(TicketRunRow row) =>
        new(row.Run.Id, row.Run.Pipeline, row.Run.Status, row.Run.StartedAt, row.Run.FinishedAt,
            row.Run.CostTotalUsd, NeverStarted: row.Run.Pipeline == PipelinePresets.CodeName);
}
