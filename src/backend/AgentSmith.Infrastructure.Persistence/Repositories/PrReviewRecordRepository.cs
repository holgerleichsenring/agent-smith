using AgentSmith.Contracts.Reviews;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// 2026-10-13-9a59b: reads the pull-request records — a ticket's with every entry, every ticket's
/// with only the standing decisions (the ticket list buckets on them), and one pull request by its
/// key or a repository's by its RepoKey for the PR sweep, the stale ones for the re-read. Writes: <see cref="PrReviewRecordWriter"/>.
/// </summary>
public sealed class PrReviewRecordRepository(IUnitOfWork unitOfWork) : IPrReviewRecordReader
{
    public Task<IReadOnlyList<PrReviewRecordView>> ByTicketAsync(string project, string ticketId, CancellationToken cancellationToken) =>
        ReadAsync(Records().Where(r => r.Project == project && r.TicketId == ticketId), standingOnly: false, cancellationToken);

    /// <summary>Every record of the project (all projects when null), entries cut to the standing decisions.</summary>
    public Task<IReadOnlyList<PrReviewRecordView>> ForListAsync(string? project, CancellationToken ct) =>
        ReadAsync(string.IsNullOrEmpty(project) ? Records() : Records().Where(r => r.Project == project), standingOnly: true, ct);

    public async Task<PrReviewRecordView?> ByPrKeyAsync(string prKey, CancellationToken ct) =>
        (await ReadAsync(Records().Where(r => r.PrKey == prKey), standingOnly: false, ct)).SingleOrDefault();

    public Task<IReadOnlyList<PrReviewRecordView>> ByRepoKeyAsync(string repoKey, CancellationToken ct) =>
        ReadAsync(Records().Where(r => r.RepoKey == repoKey), standingOnly: false, ct);

    /// <summary>2026-10-14-dbceg: webhook projects' open and all named projects' unread records; oldest check first, no entries.</summary>
    public Task<IReadOnlyList<PrReviewRecordView>> OpenUncheckedAsync(IReadOnlyCollection<string> webhookProjects,
        IReadOnlyCollection<string> projects, long checkedBeforeTicks, int take, CancellationToken ct) =>
        OldestAsync(Records().Where(r => r.CheckedAtTicks < checkedBeforeTicks
            && ((r.State == PrReviewStates.Open && webhookProjects.Contains(r.Project))
                || (r.State == PrReviewStates.Unread && projects.Contains(r.Project)))), take, ct);

    /// <summary>2026-10-14-dbceg: merged records of an ended ticket the tracker never landed; oldest check first, no entries.</summary>
    public Task<IReadOnlyList<PrReviewRecordView>> UnlandedMergedAsync(string tracker,
        IReadOnlyCollection<string> projects, long checkedBeforeTicks, int take, CancellationToken ct) =>
        OldestAsync(Records().Where(r => r.State == PrReviewStates.Merged && r.CheckedAtTicks < checkedBeforeTicks && projects.Contains(r.Project)
            && !Records().Any(o => o.Project == r.Project && o.TicketId == r.TicketId
                && o.State != PrReviewStates.Merged && o.State != PrReviewStates.Closed)
            && !unitOfWork.Set<LandedTicket>().Any(l => l.Tracker == tracker && l.TicketId == r.TicketId)), take, ct);

    private static async Task<IReadOnlyList<PrReviewRecordView>> OldestAsync(IQueryable<PrReviewRecord> query, int take, CancellationToken ct) =>
        [.. (await query.OrderBy(r => r.CheckedAtTicks).ThenBy(r => r.Id).Take(take).ToListAsync(ct)).Select(r => View(r, null, []))];

    private IQueryable<PrReviewRecord> Records() => unitOfWork.Set<PrReviewRecord>().AsNoTracking();

    private async Task<IReadOnlyList<PrReviewRecordView>> ReadAsync(IQueryable<PrReviewRecord> query, bool standingOnly, CancellationToken ct)
    {
        var records = await query.OrderBy(r => r.Id).ToListAsync(ct);
        if (records.Count == 0) return [];
        var keys = query.Select(r => r.PrKey);
        var entries = unitOfWork.Set<PrReviewEntry>().AsNoTracking().Where(e => keys.Contains(e.PrKey));
        var newest = (await entries.GroupBy(e => e.PrKey).Select(g => new { g.Key, At = g.Max(e => e.AtTicks) }).ToListAsync(ct))
            .ToDictionary(x => x.Key, x => x.At, StringComparer.Ordinal);
        var kept = await (standingOnly ? entries.Where(e => e.Standing) : entries).OrderBy(e => e.AtTicks).ThenBy(e => e.Id).ToListAsync(ct);
        var byKey = kept.ToLookup(e => e.PrKey, StringComparer.Ordinal);
        return [.. records.Select(r => View(r, newest.TryGetValue(r.PrKey, out var at) ? at : null, byKey[r.PrKey]))];
    }

    private static PrReviewRecordView View(PrReviewRecord r, long? newestEntry, IEnumerable<PrReviewEntry> entries) =>
        new(r.PrKey, r.Url, r.Repo, r.Number, r.Project, r.TicketId, r.RunId, r.State, Time(r.StateAtTicks),
            Time(r.CheckedAtTicks)!.Value, Time(newestEntry), [.. entries.Select(Entry)]);

    private static PrReviewEntryView Entry(PrReviewEntry e) =>
        new(e.HostId, e.Kind, e.Decision, e.Standing, e.AuthorLogin, e.Trusted, e.IsOurs, e.IsBot, e.IsSystem,
            e.File, e.Line, e.Resolved, e.ThreadHostId, e.Body, e.Truncated, Time(e.AtTicks)!.Value);

    private static DateTimeOffset? Time(long? ticks) => ticks is { } t ? new DateTimeOffset(t, TimeSpan.Zero) : null;
}
