using System.Collections.Concurrent;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Persistence;

/// <summary>
/// p0393a: the DB-free default (CLI, tests, dev). The relational registration replaces this.
/// <para>2026-11-28-db88a: the stored set lives beside the pointer, written and read apart from it, as on the row. It
/// is serialized like the row's, so a set read back is a copy the caller cannot change under the store. Both live as long
/// as the process: a CLI run in a new process finds no set and starts the ticket over, as the spec's scope states.</para>
/// </summary>
public sealed class InMemorySpecSetPointerStore : ISpecSetPointerStore
{
    private readonly ConcurrentDictionary<string, SpecSetPointer> _pointers = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> _sets = new(StringComparer.Ordinal);

    public Task<SpecSetPointer?> GetAsync(string project, string key, CancellationToken cancellationToken) =>
        Task.FromResult(_pointers.TryGetValue(Id(project, key), out var pointer) ? pointer : null);

    public Task SaveAsync(string project, SpecSetPointer pointer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pointer);
        _pointers[Id(project, pointer.Key)] = pointer;
        return Task.CompletedTask;
    }

    public Task<SpecSet?> GetSetAsync(string project, string key, CancellationToken cancellationToken) =>
        Task.FromResult(_sets.TryGetValue(Id(project, key), out var json) ? SpecSetJson.Read(json) : null);

    public Task SaveSetAsync(string project, SpecSet set, CancellationToken cancellationToken, bool onlyIfAbsent = false)
    {
        ArgumentNullException.ThrowIfNull(set);
        var json = SpecSetJson.Write(set);
        if (onlyIfAbsent) _sets.TryAdd(Id(project, set.Key), json);
        else _sets[Id(project, set.Key)] = json;
        // As the row does: a set saved for a ticket with no pointer creates one with nothing recorded yet.
        _pointers.TryAdd(Id(project, set.Key), new SpecSetPointer(set.Key, string.Empty, string.Empty, 0, SeriesId: set.Series));
        return Task.CompletedTask;
    }

    private static string Id(string project, string key) => $"{project} {key}";
}
