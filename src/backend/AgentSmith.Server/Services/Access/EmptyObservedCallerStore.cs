using AgentSmith.Contracts.Models.Access;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Server.Services.Access;

/// <summary>
/// 2026-08-26-7a51: the default store for a graph with no relational persistence — the CLI
/// composition and the route-table guard, which build the server without a database.
/// <para>
/// The access surface still works against it: nobody has been observed, so nobody is
/// offered to pick, and a role is granted to a value typed by hand. Registering nothing at
/// all would instead fail the route enumeration on an unresolvable handler parameter.
/// </para>
/// </summary>
internal sealed class EmptyObservedCallerStore : IObservedCallerStore
{
    public Task UpsertAsync(IReadOnlyList<ObservedCaller> callers, CancellationToken ct) =>
        Task.CompletedTask;

    public Task<IReadOnlyList<ObservedCaller>> AllAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ObservedCaller>>([]);

    public Task<bool> RemoveAsync(string subject, CancellationToken ct) => Task.FromResult(false);

    public Task<int> RemoveSeenBeforeAsync(DateTimeOffset cut, CancellationToken ct) => Task.FromResult(0);
}
