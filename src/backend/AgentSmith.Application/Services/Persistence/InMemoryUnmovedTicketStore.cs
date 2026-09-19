using System.Collections.Concurrent;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Persistence;

/// <summary>
/// 2026-09-18-c1a7: the DB-free default (CLI, tests, dev). It keeps the fact for the life of
/// the process and has no configuration documents to watch, so the release an operator gets
/// from editing the tracker or the project is the relational store's — which is where the
/// claim service that reads this runs. The CLI has no poller to stop.
/// </summary>
public sealed class InMemoryUnmovedTicketStore : IUnmovedTicketStore
{
    private readonly ConcurrentDictionary<string, UnmovedTicketFact> _facts = new(StringComparer.Ordinal);

    public Task RecordAsync(UnmovedTicketFact fact, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fact);
        _facts[Id(fact.Project, fact.TicketId)] = fact;
        return Task.CompletedTask;
    }

    public Task<UnmovedTicketFact?> FindStandingAsync(
        string project, string ticketId, string tracker, CancellationToken cancellationToken) =>
        Task.FromResult(_facts.TryGetValue(Id(project, ticketId), out var fact)
            && string.Equals(fact.Tracker, tracker, StringComparison.Ordinal) ? fact : null);

    public Task ClearAsync(string project, string ticketId, CancellationToken cancellationToken)
    {
        _facts.TryRemove(Id(project, ticketId), out _);
        return Task.CompletedTask;
    }

    private static string Id(string project, string ticketId) => $"{project} {ticketId}";
}
