using System.ComponentModel;
using System.Text.Json;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Series;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Series;

/// <summary>2026-11-17-0207a: the series master's reads over the beat's facts — the table, and one ticket's facts.</summary>
/// <para>2026-11-26-a12ed: read_ticket carries the newest run's own report, read when it is asked for.</para>
public sealed class SeriesReadTools(SeriesBeat beat, AgentSmith.Contracts.Runs.IRunReportReader? reports = null)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public IEnumerable<AIFunction> GetTools() =>
    [
        AIFunctionFactory.Create(SeriesStatus, "series_status"),
        AIFunctionFactory.Create(ReadTicketAsync, "read_ticket"),
    ];

    // 2026-11-25-9992a: the one state the series page shows — kind, words, CI and the hold — and what is at work now.
    // 2026-11-25-9992i: and the series' review (end, each, none), with its series branch when it has one.
    [Description("The series' tickets as a table: ticket, what, state, kind (merged, now, needed, question, approval — a person approves its pull request on the host, draft — its pull request is still a draft, failed, running, pr, waiting), CI, cost, time, the tickets each requires, the tickets at work now, and the series' state, spend, review (end: you merge each ticket into the series branch and a person reviews the final pull request; each: you merge a ticket once a person approved its pull request; none: you merge on green) and series branch.")]
    public string SeriesStatus() => JsonSerializer.Serialize(new
    {
        series = beat.Record.Id, state = beat.Record.State, masterCostUsd = beat.Record.MasterCostUsd,
        review = ReviewModeNames.Of(beat.Record.EffectiveReview), seriesBranch = beat.Record.IntegrationBranch,
        workingOn = beat.WorkingOn,
        tickets = beat.Rows.Zip(beat.Facts, (r, f) => new
        {
            r.Ticket, r.What, r.State, r.Kind, ci = f.Ci, hold = f.Hold?.Case, r.Cost, r.Time, requires = Keys(f.Requires),
        }),
    }, Json);

    [Description("One ticket's facts: its state and kind, its hold (who moves it and why, since when), its newest run, that run's status and why it ended, its pull requests per repo with CI on their heads, conflicts, whether each is a draft, who approved them and whether the host still needs a person's approval, its cost, when it started and landed, a question its run is parked on, your own last act on it with its reason, and report: the newest run's own account as its coding masters wrote it — what they delivered and how they checked it, review findings, deviations, follow-ups and what is open.")]
    public async Task<string> ReadTicketAsync([Description("The ticket's key or id as the table shows it.")] string ticket,
        CancellationToken ct = default) =>
        SeriesTicketLookup.Find(beat, ticket) is { } fact
            ? JsonSerializer.Serialize(new
            {
                ticket = fact.Ticket.Key, fact.Ticket.TicketId, fact.Ticket.Title, fact.State, fact.Kind, fact.Ci, fact.CiFailure,
                hold = fact.Hold is { } h ? new { h.Addressee, h.Case, h.Since, h.Names } : null,
                fact.LatestRunId, fact.LatestStatus, fact.LatestFinished, fact.EndDetail,
                pullRequests = fact.PullRequests.Select(p => new { p.Repo, p.Number, p.Url, p.State, p.Ci, p.CiFailure, p.HasConflicts, p.IsDraft, p.Target, p.ApprovedBy, p.ApprovalRequired }),
                fact.CostUsd, fact.FirstStartedAt, fact.LandedAt, requires = Keys(fact.Requires), question = fact.PendingQuestion?.Text,
                lastAct = fact.LastAct, // 2026-11-25-9992e
                report = reports is not null && fact.LatestRunId is { } run ? await reports.ReportAsync(run, ct) : null, // a12ed
            }, Json)
            : SeriesTicketLookup.Unknown(beat, ticket);

    private IReadOnlyList<string> Keys(IReadOnlyList<string> ids) =>
        [.. ids.Select(id => beat.Facts.FirstOrDefault(f => f.Ticket.TicketId == id)?.Ticket.Key ?? id)];
}
