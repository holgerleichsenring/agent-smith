using System.Collections.Concurrent;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Persistence;

/// <summary>
/// 2026-09-25-b4d9: the DB-free default (CLI, tests, dev). It keeps the record for the life
/// of the process, which is all a composition without a database can offer — there the lease
/// is a no-op and no reaper runs, so nothing outlives the process to be recovered anyway.
/// </summary>
public sealed class InMemoryTakenTicketStore : ITakenTicketStore
{
    private readonly ConcurrentDictionary<string, (TakenTicketFact Fact, TakenTicketState State)> _taken =
        new(StringComparer.Ordinal);

    public Task TakeAsync(TakenTicketFact fact, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fact);
        _taken[Id(fact.Project, fact.TicketId)] = (fact, TakenTicketState.Taken);
        return Task.CompletedTask;
    }

    public Task ClearAsync(string project, string ticketId, CancellationToken cancellationToken)
    {
        _taken.TryRemove(Id(project, ticketId), out _);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<TakenTicketFact>> ListReconcilableAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<TakenTicketFact>>(
            [.. _taken.Values.Where(e => e.State == TakenTicketState.Taken).Select(e => e.Fact)]);

    public Task<bool> TryBeginReapAsync(string project, string ticketId, CancellationToken cancellationToken)
    {
        if (!_taken.TryGetValue(Id(project, ticketId), out var entry))
            return Task.FromResult(true); // no record: nobody else can be holding this ticket
        if (entry.State == TakenTicketState.Reaping) return Task.FromResult(false);
        _taken[Id(project, ticketId)] = (entry.Fact, TakenTicketState.Reaping);
        return Task.FromResult(true);
    }

    public Task EndReapAsync(string project, string ticketId, CancellationToken cancellationToken)
    {
        if (_taken.TryGetValue(Id(project, ticketId), out var entry))
            _taken[Id(project, ticketId)] = (entry.Fact, TakenTicketState.Taken);
        return Task.CompletedTask;
    }

    private static string Id(string project, string ticketId) => $"{project} {ticketId}";
}
