using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.Tickets;

/// <summary>
/// 2026-10-13-9a59a: the ticket list — every (project, ticket id) with a code run, bucketed in
/// memory, sorted by its newest attempt's run id (immutable and SQLite-safe) and paged with that id
/// as the cursor. Buckets are computed in memory, so a page needs every ticket's newest attempt
/// anyway; the read is bounded by <see cref="TicketListQuery.Ceiling"/> instead.
/// <para>2026-10-13-9a59b: every ticket's pull-request records feed its bucket; only the page's rows
/// carry when each pull request was opened, and only the page nudges a backfill.</para>
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
    TicketAttemptRecords attemptRecords)
{
    private static readonly ILookup<string, string> NoSpecs =
        Array.Empty<string>().ToLookup(s => s, StringComparer.Ordinal);

    public async Task<TicketListView> ReadAsync(TicketListRequest request, CancellationToken ct)
    {
        var read = await query.ReadAsync(request.Project, ct);
        var tickets = read.Rows.GroupBy(r => (r.Run.Project, r.Run.TicketId)).Select(g => g.ToList()).ToList();
        var state = await live.ReadAsync([.. tickets.Select(t => attempts.Head(t).Run)], ct);
        var prRecords = await pullRequests.AllAsync(request.Project, ct);
        var all = tickets.Select(t => (Rows: t, Summary: composer.Compose(t, state, NoSpecs, pullRequests.Views(prRecords[Key(t)]))))
            .OrderByDescending(t => t.Summary.NewestRunId, StringComparer.Ordinal).ToList();
        var page = Page(all, request);
        var pageAttempts = page.SelectMany(t => attempts.Of(t.Rows)).Select(a => a.Run.Id).ToList();
        var statuses = await specs.ForAsync(pageAttempts, ct);
        var records = await attemptRecords.ForAsync(pageAttempts, ct);
        var pagePrs = await pullRequests.PageAsync([.. page.SelectMany(t => t.Rows).Select(r => r.Run.Id)], prRecords, ct);
        return new TicketListView(
            [.. page.Select(t => composer.Compose(t.Rows, state, statuses, pagePrs.GetValueOrDefault(Key(t.Rows)) ?? [], records))],
            NextCursor(all, page, request),
            counts.Of([.. all.Select(t => t.Summary)], await monthCost.OfAsync(request.Project, ct), read.Truncated));
    }

    private static (string Project, string TicketId) Key(List<TicketRunRow> rows) => (rows[0].Run.Project, rows[0].Run.TicketId);

    private static List<(List<TicketRunRow> Rows, TicketSummaryView Summary)> Page(
        List<(List<TicketRunRow> Rows, TicketSummaryView Summary)> all, TicketListRequest request) =>
        [.. Filtered(all, request).Take(request.PageSize)];

    private static IEnumerable<(List<TicketRunRow> Rows, TicketSummaryView Summary)> Filtered(
        List<(List<TicketRunRow> Rows, TicketSummaryView Summary)> all, TicketListRequest request) =>
        all.Where(t => string.IsNullOrEmpty(request.Bucket) || t.Summary.Bucket == request.Bucket)
            .Where(t => string.IsNullOrEmpty(request.Cursor)
                || string.CompareOrdinal(t.Summary.NewestRunId, request.Cursor) < 0);

    private static string? NextCursor(
        List<(List<TicketRunRow> Rows, TicketSummaryView Summary)> all,
        List<(List<TicketRunRow> Rows, TicketSummaryView Summary)> page, TicketListRequest request) =>
        page.Count > 0 && Filtered(all, request).Count() > page.Count ? page[^1].Summary.NewestRunId : null;
}
