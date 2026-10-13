using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Services.Tickets;

/// <summary>
/// 2026-10-13-9a59b: the pull-request records the ticket reads show, and the backfill they trigger.
/// The list reads every record once (its buckets need them) and the page's run JSON for when each
/// pull request was opened; a page ticket whose runs name a pull request with no record is nudged
/// for a refresh. The detail nudges as well when its records were checked over five minutes ago.
/// Reads only nudge — the nudge worker is the only writer.
/// </summary>
public sealed class TicketPullRequestReader(
    IServiceScopeFactory scopes, IReworkNudges nudges, TicketReviewState review, TimeProvider clock)
{
    private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(5);
    private static readonly IReadOnlyDictionary<string, DateTimeOffset> NotOpened = new Dictionary<string, DateTimeOffset>();

    public async Task<ILookup<(string Project, string TicketId), PrReviewRecordView>> AllAsync(string? project, CancellationToken ct)
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
                     .Select(n => (n.Project, n.TicketId)).Distinct())
            await NudgeAsync(ticket.Project, ticket.TicketId, ct);
        return named.Select(n => (n.Project, n.TicketId)).Concat(records.Select(g => g.Key)).Distinct()
            .ToDictionary(k => k, k => (IReadOnlyList<TicketPullRequestView>)review.Of(records[k], opened));
    }

    /// <summary>One ticket's pull requests; a missing or stale record nudges one refresh.</summary>
    public async Task<IReadOnlyList<TicketPullRequestView>> TicketAsync(string project, string ticketId, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var records = await scope.ServiceProvider.GetRequiredService<PrReviewRecordRepository>().ByTicketAsync(project, ticketId, ct);
        var named = await scope.ServiceProvider.GetRequiredService<NamedPullRequestReader>().ByTicketAsync(project, ticketId, ct);
        var stale = clock.GetUtcNow() - StaleAfter;
        if (named.Any(n => records.All(r => r.PrKey != PrKey.Of(n.Url))) || records.Any(r => r.CheckedAt < stale))
            await NudgeAsync(project, ticketId, ct);
        return review.Of(records, OpenedAt(named));
    }

    private static Dictionary<string, DateTimeOffset> OpenedAt(IEnumerable<NamedPullRequest> named) =>
        named.ToDictionary(n => PrKey.Of(n.Url), n => n.OpenedAt, StringComparer.Ordinal);

    private Task NudgeAsync(string project, string ticketId, CancellationToken ct) =>
        nudges.EnqueueAsync(new ReworkNudgeRequest(project, ticketId, ReworkNudgeOrigin.Refresh), ct);
}
