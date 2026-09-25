using System.Collections.Concurrent;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Persistence;

/// <summary>
/// 2026-09-17-0e79a: the DB-free default (CLI, tests, dev), beside the pointer store's. A
/// process that never files an approval never reads one back, so an empty store here is the
/// honest answer — the CARRY is what gets an approved set to a run started outside the server,
/// and a filed ticket that arrives with neither fails loudly rather than deriving a guess.
/// The relational registration replaces this.
/// </summary>
public sealed class InMemorySpecApprovalStore : ISpecApprovalStore
{
    private readonly ConcurrentDictionary<string, SpecApprovalRecord> _records = new(StringComparer.Ordinal);

    private readonly ConcurrentDictionary<string, DateTimeOffset> _satisfied = new(StringComparer.Ordinal);

    public Task<SpecApprovalRecord?> GetAsync(string tracker, string key, CancellationToken cancellationToken) =>
        Task.FromResult(_records.TryGetValue(Id(tracker, key), out var record) ? record : null);

    public Task SaveAsync(SpecApprovalRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        _records[Id(record.Tracker, record.Key)] = record;
        // 2026-09-25-c1f7: a second approval is new work on the same ticket — it goes back to
        // outstanding, the same rule the relational row follows.
        _satisfied.TryRemove(Id(record.Tracker, record.Key), out _);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 2026-09-25-c1f7: oldest first, by the approval instant the record carries — this store has
    /// no row id to sort on, which is what the relational one uses because SQLite cannot order by
    /// a DateTimeOffset. The two agree except on a re-approved record. A record with no ticket id
    /// cannot be named in a tracker query and is skipped.
    /// </summary>
    public Task<OutstandingApprovals> ListOutstandingAsync(
        string tracker, int limit, CancellationToken cancellationToken)
    {
        if (limit <= 0) return Task.FromResult(OutstandingApprovals.None);
        var outstanding = _records.Values
            .Where(r => string.Equals(r.Tracker, tracker, StringComparison.Ordinal))
            .Where(r => !string.IsNullOrWhiteSpace(r.TicketId))
            .Where(r => !_satisfied.ContainsKey(Id(r.Tracker, r.Key)))
            .OrderBy(r => r.Approval?.At ?? DateTimeOffset.MinValue)
            .Select(r => r.TicketId)
            .ToList();
        return Task.FromResult(new OutstandingApprovals(
            [.. outstanding.Take(limit)], Math.Max(0, outstanding.Count - limit)));
    }

    public Task MarkSatisfiedAsync(
        string tracker, string key, DateTimeOffset at, CancellationToken cancellationToken)
    {
        if (_records.ContainsKey(Id(tracker, key))) _satisfied[Id(tracker, key)] = at;
        return Task.CompletedTask;
    }

    private static string Id(string? tracker, string? key) => $"{tracker} {key}";
}
