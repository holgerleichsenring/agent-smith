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

    public Task<SpecApprovalRecord?> GetAsync(string tracker, string key, CancellationToken cancellationToken) =>
        Task.FromResult(_records.TryGetValue(Id(tracker, key), out var record) ? record : null);

    public Task SaveAsync(SpecApprovalRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        _records[Id(record.Tracker, record.Key)] = record;
        return Task.CompletedTask;
    }

    private static string Id(string? tracker, string? key) => $"{tracker} {key}";
}
