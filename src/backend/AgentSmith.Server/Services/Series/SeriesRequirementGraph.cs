using AgentSmith.Contracts.Tickets;

namespace AgentSmith.Server.Services.Series;

/// <summary>2026-11-25-9992f: a project's requirement edges as a graph of ticket ids — which ticket waits on which — so an
/// edge the series master asks for is checked against closing a cycle before anything is written.</summary>
public sealed class SeriesRequirementGraph
{
    private readonly Dictionary<string, HashSet<string>> _requires = new(StringComparer.Ordinal);

    public SeriesRequirementGraph(IEnumerable<TicketRequirementEdge> edges)
    {
        ArgumentNullException.ThrowIfNull(edges);
        foreach (var edge in edges) Add(edge.TicketId, edge.RequiredTicketId);
    }

    public void Add(string dependent, string required)
    {
        if (!_requires.TryGetValue(dependent, out var set)) _requires[dependent] = set = new HashSet<string>(StringComparer.Ordinal);
        set.Add(required);
    }

    public void Remove(string dependent, string required)
    {
        if (_requires.TryGetValue(dependent, out var set)) set.Remove(required);
    }

    /// <summary>Whether <paramref name="from"/> waits on <paramref name="to"/>, directly or through other tickets; a ticket
    /// reaches itself.</summary>
    public bool Reaches(string from, string to)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var next = new Queue<string>([from]);
        while (next.TryDequeue(out var ticket))
        {
            if (string.Equals(ticket, to, StringComparison.Ordinal)) return true;
            if (!seen.Add(ticket) || !_requires.TryGetValue(ticket, out var set)) continue;
            foreach (var required in set) next.Enqueue(required);
        }
        return false;
    }
}
