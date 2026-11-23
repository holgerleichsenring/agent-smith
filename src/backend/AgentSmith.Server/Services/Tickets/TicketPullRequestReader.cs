using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Server.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Tickets;

/// <summary>
/// 2026-10-13-9a59b: the pull-request records the ticket reads show, and the backfill they trigger.
/// The list reads every record once (its buckets need them) and the page's run JSON for when each
/// pull request was opened; a page ticket whose runs name a pull request with no record is nudged
/// for a refresh. The detail nudges as well when its records were checked over five minutes ago.
/// Reads only nudge — the nudge worker is the only writer.
/// <para>2026-11-23-1e76a: a nudge is an upsert, and the page re-reads every two seconds during a live run, so each ticket
/// is nudged at most once per <see cref="StaleAfter"/> for each reason — a stale record, or the same set of pull requests
/// without one — on this replica, as <see cref="Checks.TicketCheckKicks"/> throttles its kicks. 2026-11-22-0b23: a nudge that cannot be
/// written is logged, and the read answers anyway.</para>
/// </summary>
public sealed class TicketPullRequestReader(
    IServiceScopeFactory scopes, IReworkNudges nudges, TicketReviewState review, TimeProvider clock, IMemoryCache nudged,
    ILogger<TicketPullRequestReader> logger)
{
    private const string Stale = "stale";

    private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(5);
    private static readonly IReadOnlyDictionary<string, DateTimeOffset> NotOpened = new Dictionary<string, DateTimeOffset>();

    public async Task<ILookup<(string Project, string TicketId), PrReviewRecordView>> AllAsync(ProjectScope project, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var records = await scope.ServiceProvider.GetRequiredService<PrReviewRecordRepository>().ForListAsync(project, ct);
        return records.ToLookup(r => (r.Project, r.TicketId));
    }

    /// <summary>A ticket's pull requests from records read for the whole list — no opened-at.</summary>
    public IReadOnlyList<TicketPullRequestView> Views(IEnumerable<PrReviewRecordView> records) => review.Of(records, NotOpened);

    /// <summary>The page tickets' pull requests, keyed by ticket; tickets with an unrecorded pull request are nudged.</summary>
    public async Task<IReadOnlyDictionary<(string Project, string TicketId), IReadOnlyList<TicketPullRequestView>>> PageAsync(
        IReadOnlyList<string> codeRunIds, ILookup<(string Project, string TicketId), PrReviewRecordView> records, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var named = await scope.ServiceProvider.GetRequiredService<NamedPullRequestReader>().ByRunsAsync(codeRunIds, ct);
        var opened = OpenedAt(named);
        foreach (var ticket in named.Where(n => !records[(n.Project, n.TicketId)].Any(r => r.PrKey == PrKey.Of(n.Url)))
                     .GroupBy(n => (n.Project, n.TicketId)))
            await NudgeAsync(ticket.Key.Project, ticket.Key.TicketId, Unrecorded(ticket), ct);
        return named.Select(n => (n.Project, n.TicketId)).Concat(records.Select(g => g.Key)).Distinct()
            .ToDictionary(k => k, k => (IReadOnlyList<TicketPullRequestView>)review.Of(records[k], opened));
    }

    /// <summary>One ticket's pull requests; a missing or stale record nudges one refresh.</summary>
    public async Task<IReadOnlyList<TicketPullRequestView>> TicketAsync(string project, string ticketId, CancellationToken ct) =>
        (await TicketRecordsAsync(project, ticketId, ct)).Views;

    /// <summary>2026-10-13-9a59h: the same read with the records and their entries beside the views.</summary>
    public async Task<(IReadOnlyList<PrReviewRecordView> Records, IReadOnlyList<TicketPullRequestView> Views)> TicketRecordsAsync(
        string project, string ticketId, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var records = await scope.ServiceProvider.GetRequiredService<PrReviewRecordRepository>().ByTicketAsync(project, ticketId, ct);
        var named = await scope.ServiceProvider.GetRequiredService<NamedPullRequestReader>().ByTicketAsync(project, ticketId, ct);
        var stale = clock.GetUtcNow() - StaleAfter;
        var unrecorded = named.Where(n => records.All(r => r.PrKey != PrKey.Of(n.Url))).ToList();
        if (unrecorded.Count > 0) await NudgeAsync(project, ticketId, Unrecorded(unrecorded), ct);
        else if (records.Any(r => r.CheckedAt < stale)) await NudgeAsync(project, ticketId, Stale, ct);
        return (records, review.Of(records, OpenedAt(named)));
    }

    private static string Unrecorded(IEnumerable<NamedPullRequest> named) =>
        "unrecorded:" + string.Join(",", named.Select(n => PrKey.Of(n.Url)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));

    private static Dictionary<string, DateTimeOffset> OpenedAt(IEnumerable<NamedPullRequest> named) =>
        named.ToDictionary(n => PrKey.Of(n.Url), n => n.OpenedAt, StringComparer.Ordinal);

    private async Task NudgeAsync(string project, string ticketId, string reason, CancellationToken ct)
    {
        var key = $"pr-refresh-nudge:{reason}:{project}/{ticketId}";
        if (nudged.TryGetValue(key, out _)) return;
        try
        {
            await nudges.EnqueueAsync(new ReworkNudgeRequest(project, ticketId, ReworkNudgeOrigin.Refresh), ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested) // 2026-11-22-0b23: the read answers without its nudge
        {
            logger.LogWarning(ex, "The refresh nudge of ticket {Ticket} in {Project} was not written; the next read tries again",
                ticketId, project);
            return;
        }
        nudged.Set(key, true, StaleAfter); // marked once enqueued, so a failed write is tried again on the next read
    }
}
