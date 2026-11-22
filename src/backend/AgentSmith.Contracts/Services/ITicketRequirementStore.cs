using AgentSmith.Contracts.Tickets;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-10-14-dbcef: which filed ticket must wait for which other ticket to be merged. Insert only:
/// an edge is written once at filing and outlives every run of both tickets.
/// </summary>
public interface ITicketRequirementStore
{
    /// <summary>
    /// Stores the edges of ONE dependent ticket in one transaction and answers how many this call
    /// added. An edge already stored is skipped. Throws <see cref="ArgumentException"/> for a batch
    /// naming two dependents, an edge requiring its own ticket, or edges disagreeing on StartsWhenMet.
    /// </summary>
    Task<int> AddAsync(IReadOnlyList<TicketRequirementEdge> edges, CancellationToken cancellationToken);

    /// <summary>The edges of the dependent ticket — what it waits for; empty for an unknown ticket.</summary>
    Task<IReadOnlyList<TicketRequirementEdge>> RequiredOfAsync(
        string tracker, string ticketKey, CancellationToken cancellationToken);

    /// <summary>2026-11-22-9719d: <see cref="RequiredOfAsync"/> for many dependents of one tracker connection; every
    /// asked key is present, with no edges when it has none.</summary>
    async Task<IReadOnlyDictionary<string, IReadOnlyList<TicketRequirementEdge>>> RequiredOfManyAsync(
        string tracker, IReadOnlyCollection<string> ticketKeys, CancellationToken cancellationToken)
    {
        var byKey = new Dictionary<string, IReadOnlyList<TicketRequirementEdge>>(StringComparer.Ordinal);
        foreach (var key in ticketKeys)
            if (!byKey.ContainsKey(key)) byKey[key] = await RequiredOfAsync(tracker, key, cancellationToken);
        return byKey;
    }

    /// <summary>Every edge naming the ticket as required — who waits for it; empty for an unknown ticket.</summary>
    Task<IReadOnlyList<TicketRequirementEdge>> DependentsOfAsync(
        string tracker, string requiredTicketKey, CancellationToken cancellationToken);

    /// <summary>Every edge whose dependent was filed into the project; empty for an unknown project.</summary>
    Task<IReadOnlyList<TicketRequirementEdge>> ByProjectAsync(string project, CancellationToken cancellationToken);
}
