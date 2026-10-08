using AgentSmith.Contracts.Specs;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// 2026-09-17-0e79a: data access for the approved record over a scoped unit of work. One row
/// per ticket key — approving the same ticket again upserts in place, because the row answers
/// "what is approved for this ticket now", and the branch history answers "what happened".
/// 2026-10-06-03c7f: the row is the approved SERIES (<see cref="ApprovedSeries"/>).
/// </summary>
public sealed class ApprovedSeriesRepository(IUnitOfWork unitOfWork)
{
    private const string RepositorySeparator = ",";

    public async Task<SpecApprovalRecord?> GetAsync(string tracker, string key, CancellationToken ct)
    {
        var row = await unitOfWork.Set<ApprovedSeries>().AsNoTracking()
            .FirstOrDefaultAsync(a => a.Tracker == tracker && a.TicketKey == key, ct);
        return row is null ? null : SpecApprovalJson.Read(row.ContentJson);
    }

    public async Task SaveAsync(SpecApprovalRecord record, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(record);
        var existing = await unitOfWork.Set<ApprovedSeries>()
            .FirstOrDefaultAsync(a => a.Tracker == record.Tracker && a.TicketKey == record.Key, ct);
        if (existing is null)
        {
            existing = new ApprovedSeries { TicketKey = record.Key, Tracker = record.Tracker };
            unitOfWork.Add(existing);
        }
        Apply(record, existing);
        await unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>
    /// 2026-09-25-c1f7: the tracker's unsatisfied records, OLDEST FIRST, with a count of what the
    /// limit left out. A row whose TicketId is empty predates the column and is skipped: nothing
    /// here can recover the tracker's own spelling from the spec key, and naming a guess in a JQL
    /// clause fetches the wrong ticket rather than none.
    /// <para>
    /// "Oldest" is the row ID and not ApprovedAt, because SQLite cannot ORDER BY a DateTimeOffset
    /// and the ordering has to happen in the database — ordering after the take would bound the
    /// wrong set. The id is the insertion order, so the two agree except on a record whose second
    /// approval upserted in place, which keeps the position its first approval gave it.
    /// </para>
    /// </summary>
    public async Task<OutstandingApprovals> ListOutstandingAsync(
        string tracker, int limit, CancellationToken ct)
    {
        if (limit <= 0) return OutstandingApprovals.None;
        var outstanding = unitOfWork.Set<ApprovedSeries>().AsNoTracking()
            .Where(a => a.Tracker == tracker && a.SatisfiedAt == null && a.TicketId != "")
            .OrderBy(a => a.Id);
        var total = await outstanding.CountAsync(ct);
        var ids = await outstanding.Take(limit).Select(a => a.TicketId).ToListAsync(ct);
        return new OutstandingApprovals(ids, Math.Max(0, total - ids.Count));
    }

    /// <summary>2026-09-25-c1f7: a run finished this ticket — stop naming it in the poll's query.</summary>
    public async Task MarkSatisfiedAsync(string tracker, string key, DateTimeOffset at, CancellationToken ct)
    {
        var row = await unitOfWork.Set<ApprovedSeries>()
            .FirstOrDefaultAsync(a => a.Tracker == tracker && a.TicketKey == key, ct);
        // A run finalizing a ticket nobody approved is the ordinary case, not an error.
        if (row is null || row.SatisfiedAt is not null) return;
        row.SatisfiedAt = at;
        await unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>2026-10-06-03c7f: a Retry is new work on the ticket — discovery names it again.</summary>
    public async Task ReopenAsync(string tracker, string key, CancellationToken ct)
    {
        var row = await unitOfWork.Set<ApprovedSeries>()
            .FirstOrDefaultAsync(a => a.Tracker == tracker && a.TicketKey == key, ct);
        if (row?.SatisfiedAt is null) return;
        row.SatisfiedAt = null;
        await unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>
    /// 2026-10-01-283df: the website sets the records approved in one conversation cite — what a
    /// delete of that conversation must keep, because the run of a filed ticket builds against them.
    /// </summary>
    public async Task<IReadOnlySet<string>> CitedSetsAsync(string conversation, CancellationToken ct)
    {
        var rows = await unitOfWork.Set<ApprovedSeries>().AsNoTracking()
            .Where(a => a.ApprovedInConversation == conversation).Select(a => a.ContentJson).ToListAsync(ct);
        // 2026-10-08-e8b9k: and the images, by the set id each is stored under.
        return rows.Select(SpecApprovalJson.Read).SelectMany(r => r is null ? [] : r.CitedSets.Concat(r.CitedImages))
            .ToHashSet(StringComparer.Ordinal);
    }

    private static void Apply(SpecApprovalRecord record, ApprovedSeries row)
    {
        row.ContentJson = SpecApprovalJson.Write(record);
        row.SeriesId = record.Set.Series ?? string.Empty;
        row.TicketId = record.TicketId;
        row.Repositories = string.Join(RepositorySeparator, record.Repositories);
        row.CarryingRepo = record.CarryingRepo;
        row.ApprovedAt = record.Approval?.At ?? default;
        row.ApprovedInConversation = record.Approval?.Conversation ?? string.Empty;
        row.ApprovedBy = record.Approval?.Principal ?? string.Empty;
        // 2026-09-25-c1f7: a second approval is NEW work on the same ticket, so the row goes back
        // to outstanding — otherwise an amendment approved after a run would never be discovered.
        row.SatisfiedAt = null;
    }
}
