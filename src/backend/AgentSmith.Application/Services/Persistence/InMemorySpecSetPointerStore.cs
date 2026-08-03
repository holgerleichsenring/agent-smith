using System.Collections.Concurrent;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Persistence;

/// <summary>
/// p0393a: the DB-free default (CLI, tests, dev). The pointer is machine state only;
/// losing it degrades a re-entry to "the set on the branch is a foreign edit", which is
/// the SAFE reading — the next revision treats it as input and never overwrites it. The
/// relational registration replaces this.
/// </summary>
public sealed class InMemorySpecSetPointerStore : ISpecSetPointerStore
{
    private readonly ConcurrentDictionary<string, SpecSetPointer> _pointers = new(StringComparer.Ordinal);

    public Task<SpecSetPointer?> GetAsync(string project, string key, CancellationToken cancellationToken) =>
        Task.FromResult(_pointers.TryGetValue(Id(project, key), out var pointer) ? pointer : null);

    public Task SaveAsync(string project, SpecSetPointer pointer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pointer);
        _pointers[Id(project, pointer.Key)] = pointer;
        return Task.CompletedTask;
    }

    private static string Id(string project, string key) => $"{project} {key}";
}
