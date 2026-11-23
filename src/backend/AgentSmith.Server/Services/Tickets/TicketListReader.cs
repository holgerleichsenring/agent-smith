using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.Tickets;

/// <summary>
/// 2026-10-13-9a59a: the ticket list — every (project, ticket id) with a code run, bucketed in
/// memory, sorted by its newest attempt's run id (immutable and SQLite-safe) and paged with that id
/// as the cursor. Buckets are computed in memory, so a page needs every ticket's newest attempt
/// anyway; the read is bounded by <see cref="TicketListQuery.Ceiling"/> instead.
/// <para>2026-10-13-9a59b: every ticket's pull-request records feed its bucket; only the page's rows
/// carry when each pull request was opened, and only the page nudges a backfill.</para>
/// <para>2026-10-13-9a59e: only the page's rows carry their series strip.</para>
/// <para>2026-10-13-9a59h: every ticket's stored check decides stuck and every hand-back row its since; the
/// tracker is never read here — the page's failed tickets are only offered to the check, unawaited.</para>
/// <para>2026-10-14-dbcen: <see cref="AllAsync"/> is the one summary read the graph shares. Never-run rows take
/// no cursor: blocked and filed ones lead the first page, a done one only under the done filter.</para>
/// <para>2026-10-15-114fb: a waits-for entry says its required ticket is closed; a row closed by hand is not checked.</para>
/// <para>2026-10-26-af69: every row's cost counts the ticket's spec derivations, a never-run row's included.</para>
/// <para>2026-11-23-31dcg: every row carries its hold, read from the inputs one read gathers for all of them
/// (<see cref="Holds.TicketHoldInputsReader"/>); the needs-you filter takes admin holds too when the request asks for them.</para>
/// </summary>
public sealed class TicketListReader(
    TicketListQuery query,
    TicketAttempts attempts,
    TicketLiveStateReader live,
    TicketSummaryComposer composer,
    TicketSpecStatuses specs,
    TicketMonthCost monthCost,
    TicketCounts counts,
    TicketPullRequestReader pullRequests,
    TicketAttemptRecords attemptRecords,
    SeriesStripReader strips,
    TicketStandings standings,
    Checks.TicketCheckKicks kicks,
    TicketLaunchEnds launchEnds,
    FiledTicketRows filed,
    TicketProjects projects,
    Holds.TicketHoldInputsReader holdInputs)
{
    private static readonly ILookup<string, string> NoSpecs =
        Array.Empty<string>().ToLookup(s => s, StringComparer.Ordinal);

    /// <summary>Every run-bearing ticket with its rows and summary, and every never-run ticket's summary.</summary>
    public async Task<TicketListAll> AllAsync(ProjectScope project, CancellationToken ct)
    {
        var config = projects.Config(); // 2026-11-22-9719d: one configuration load per read
        var configured = config.Projects;
        var inputs = await holdInputs.ReadAsync(project, configured, config.Preconditions, config.Holds, null, ct);
        var read = await query.ReadAsync(project, ct);
        var tickets = read.Rows.GroupBy(r => (r.Run.Project, r.Run.TicketId)).Select(g => g.ToList()).ToList();
        var heads = tickets.Select(t => attempts.Head(t).Run).ToList();
        var state = await live.ReadAsync(heads, ct);
        var prRecords = await pullRequests.AllAsync(project, ct);
        var standing = await standings.ReadAsync(heads, ct);
        var headLaunches = await launchEnds.ForAsync([.. heads.Select(h => h.Id)], ct); // 31dcg: a lost sandbox holds a retry here too
        var costs = read.DerivationCosts;
        var runs = tickets.Select(t => (Rows: t, Summary: composer.Compose(
                t, state, NoSpecs, pullRequests.Views(prRecords[Key(t)]), null, standing, headLaunches, costs.GetValueOrDefault(Key(t)),
                inputs)))
            .OrderByDescending(t => t.Summary.NewestRunId, StringComparer.Ordinal).ToList();
        var neverRun = (await filed.ListAsync(project, configured, ct))
            .Select(s => s with { Cost = s.Cost + costs.GetValueOrDefault((s.Project, s.TicketId)) }).ToList();
        var all = ClosedRequirements.Apply(
            new TicketListAll(runs, neverRun, state, prRecords, standing, read.Truncated, costs) { Inputs = inputs },
            p => configured.GetValueOrDefault(p));
        return all with { NeverRun = [.. all.NeverRun.Select(s => composer.HeldNeverRun(s, inputs))] }; // 31dcg: held once closed requirements are known
    }

    public async Task<TicketListView> ReadAsync(TicketListRequest request, CancellationToken ct)
    {
        var read = await AllAsync(request.Projects, ct);
        var (all, neverRun, state, prRecords, standing, truncated, costs) = read;
        var page = Page(all, request);
        kicks.Offer(page.Where(t => t.Summary.Status == RunStatuses.Failed && t.Summary.Kind != TicketClosedKinds.ByHand)
            .Select(t => new Checks.TicketCheckKick(t.Summary.Project, t.Summary.TicketId)));
        var pageAttempts = page.SelectMany(t => attempts.Of(t.Rows)).Select(a => a.Run.Id).ToList();
        var statuses = await specs.ForAsync(pageAttempts, ct);
        var records = await attemptRecords.ForAsync(pageAttempts, ct);
        var launches = await launchEnds.ForAsync(pageAttempts, ct);
        var pagePrs = await pullRequests.PageAsync([.. page.SelectMany(t => t.Rows).Select(r => r.Run.Id)], prRecords, ct);
        var pageStrips = await strips.ForAsync([.. page.Select(t => (IReadOnlyList<Run>)[.. attempts.Of(t.Rows).Select(a => a.Run)])], ct);
        var leading = string.IsNullOrEmpty(request.Cursor) ? neverRun.Where(s => Leads(s, request)) : [];
        return new TicketListView(
            [.. leading, .. page.Select(t => composer.Compose(t.Rows, state, statuses, pagePrs.GetValueOrDefault(Key(t.Rows)) ?? [],
                    records, standing, launches, costs.GetValueOrDefault(Key(t.Rows)), read.Inputs))
                .Select(s => s with { SeriesStrip = pageStrips.GetValueOrDefault(s.NewestRunId) })],
            NextCursor(all, page, request),
            counts.Of([.. all.Select(t => t.Summary), .. neverRun], await monthCost.OfAsync(request.Projects, ct), truncated));
    }

    private static bool Leads(TicketSummaryView neverRun, TicketListRequest request) => string.IsNullOrEmpty(request.Bucket)
        ? neverRun.Bucket is TicketBucket.Blocked or TicketBucket.Filed or TicketBucket.NeedsYou
        : In(neverRun, request);

    // 2026-11-23-31dcg: the needs-you filter is Your move — with admin holds when the request asks for them.
    private static bool In(TicketSummaryView summary, TicketListRequest request) =>
        summary.Bucket == request.Bucket
        || (request.AdminHolds && request.Bucket == TicketBucket.NeedsYou
            && summary.Hold?.Addressee == AgentSmith.Application.Services.Holds.TicketHoldAddressees.Admin);

    private static (string Project, string TicketId) Key(List<TicketRunRow> rows) => (rows[0].Run.Project, rows[0].Run.TicketId);

    private static List<(List<TicketRunRow> Rows, TicketSummaryView Summary)> Page(
        IReadOnlyList<(List<TicketRunRow> Rows, TicketSummaryView Summary)> all, TicketListRequest request) =>
        [.. Filtered(all, request).Take(request.PageSize)];

    private static IEnumerable<(List<TicketRunRow> Rows, TicketSummaryView Summary)> Filtered(
        IReadOnlyList<(List<TicketRunRow> Rows, TicketSummaryView Summary)> all, TicketListRequest request) =>
        all.Where(t => string.IsNullOrEmpty(request.Bucket) || In(t.Summary, request))
            .Where(t => string.IsNullOrEmpty(request.Cursor)
                || string.CompareOrdinal(t.Summary.NewestRunId, request.Cursor) < 0);

    private static string? NextCursor(
        IReadOnlyList<(List<TicketRunRow> Rows, TicketSummaryView Summary)> all,
        List<(List<TicketRunRow> Rows, TicketSummaryView Summary)> page, TicketListRequest request) =>
        page.Count > 0 && Filtered(all, request).Count() > page.Count ? page[^1].Summary.NewestRunId : null;
}
