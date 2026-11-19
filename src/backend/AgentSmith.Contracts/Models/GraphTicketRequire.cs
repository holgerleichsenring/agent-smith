namespace AgentSmith.Contracts.Models;

/// <summary>
/// 2026-10-14-dbcej: a ticket of a list requires a sibling by its key, or a ticket already filed in
/// the project's tracker as <c>ticket:&lt;id&gt;</c>. The proposal shows a numeric id as <c>#41</c>,
/// so <c>ticket:#41</c> names the same ticket as <c>ticket:41</c>.
/// </summary>
public static class GraphTicketRequire
{
    public const string ExistingPrefix = "ticket:";

    /// <summary>The tracker id after the prefix with one leading '#' stripped — empty when nothing
    /// follows; null when the requirement names a sibling.</summary>
    public static string? ExistingId(string requirement)
    {
        ArgumentNullException.ThrowIfNull(requirement);
        if (!requirement.StartsWith(ExistingPrefix, StringComparison.Ordinal)) return null;
        var id = requirement[ExistingPrefix.Length..].Trim();
        return id.StartsWith('#') ? id[1..].Trim() : id;
    }

    /// <summary>2026-11-19-46bd: the list with every <c>ticket:&lt;x&gt;</c> whose <c>&lt;x&gt;</c> is the key of a
    /// ticket in the same list (ordinal) written as that sibling's bare key — a key of the list is named, so it is the
    /// sibling, never a tracker id. A ticket that names none keeps its instance; a list that names none is returned.</summary>
    public static IReadOnlyList<GraphTicket> Siblings(IReadOnlyList<GraphTicket> tickets)
    {
        ArgumentNullException.ThrowIfNull(tickets);
        var keys = tickets.Select(t => t.Key).ToHashSet(StringComparer.Ordinal);
        bool Sibling(string requirement) => ExistingId(requirement) is { Length: > 0 } id && keys.Contains(id);
        if (!tickets.Any(t => t.Requires.Any(Sibling))) return tickets;
        return [.. tickets.Select(t => t.Requires.Any(Sibling)
            ? t with { Requires = [.. t.Requires.Select(r => Sibling(r) ? ExistingId(r)! : r)] }
            : t)];
    }
}
