using System.Text.RegularExpressions;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-10-14-dbcej: whether a proposed list of tickets can be filed as it stands — the count, each
/// key, each ticket's fields within <see cref="TicketGraphLimits"/>, each requires, each repository
/// and the order the edges allow, checked in that order. Every reason names the ticket and the rule
/// so the design partner's one re-prompt can fix it. A done item is one line because the filed
/// body lists it under the acceptance criteria, where only a list item reads back as a criterion.
/// </summary>
public sealed partial class TicketGraphChecker(TicketGraphOrderer orderer)
{
    [GeneratedRegex("\\A[a-z][a-z0-9-]{1,40}\\z")]
    private static partial Regex KeyShape();

    /// <summary>Why the list is refused, or null when it can be filed.</summary>
    /// <param name="scope">The turn's repository names; empty when no scope is known.</param>
    public string? Check(TicketGraphOutcome graph, IReadOnlyList<string> scope)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(scope);
        var tickets = graph.Tickets;
        if (tickets.Count < TicketGraphLimits.MinTickets)
            return $"the tickets outcome has {tickets.Count} ticket(s) and a list holds at least "
                + $"{TicketGraphLimits.MinTickets} — a single piece of work is a spec or a bug";
        if (tickets.Count > TicketGraphLimits.MaxTickets)
            return $"the tickets outcome has {tickets.Count} tickets and a list holds at most {TicketGraphLimits.MaxTickets} — "
                + $"propose the first {TicketGraphLimits.MaxTickets} now and name them with ticket:<id> in the next approval";
        if (Keys(tickets) is { } keyError) return keyError;
        var siblings = tickets.Select(t => t.Key).ToHashSet(StringComparer.Ordinal);
        foreach (var ticket in tickets)
            if ((Fields(ticket) ?? Requires(ticket, siblings) ?? Repos(ticket, scope)) is { } error) return error;
        return orderer.Order(tickets).Error;
    }

    private static string? Keys(IReadOnlyList<GraphTicket> tickets)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < tickets.Count; i++)
        {
            var key = tickets[i].Key;
            if (key.Length == 0) return $"ticket #{i + 1} has no 'key'";
            if (!KeyShape().IsMatch(key))
                return $"ticket #{i + 1} has the key '{key}' — a key is a lower-case letter followed by 1 to 40 "
                    + "lower-case letters, digits or '-'";
            if (!seen.Add(key)) return $"ticket '{key}' is named twice — every key is unique in the list";
        }
        return null;
    }

    private static string? Fields(GraphTicket t)
    {
        if (t.Title.Length == 0) return $"ticket '{t.Key}' has no 'title'";
        if (t.Title.Contains('\n')) return $"ticket '{t.Key}': 'title' spans several lines — a title is one line";
        if (t.Title.Length > TicketGraphLimits.MaxTitle) return Over(t, "title", t.Title.Length, TicketGraphLimits.MaxTitle);
        if (t.Requirement.Length == 0) return $"ticket '{t.Key}' has no 'requirement' — state its scope in prose";
        if (t.Requirement.Length > TicketGraphLimits.MaxRequirement)
            return Over(t, "requirement", t.Requirement.Length, TicketGraphLimits.MaxRequirement);
        if (t.Done.Count == 0)
            return $"ticket '{t.Key}' has no 'done' item — name at least one outcome that is true once it has merged";
        if (t.Done.Count > TicketGraphLimits.MaxDoneItems)
            return $"ticket '{t.Key}' has {t.Done.Count} 'done' items and a ticket holds at most {TicketGraphLimits.MaxDoneItems}";
        foreach (var item in t.Done)
        {
            if (item.Length == 0) return $"ticket '{t.Key}' has an empty 'done' item";
            if (item.Contains('\n')) return $"ticket '{t.Key}': a 'done' item spans several lines — each item is one line";
            if (item.Length > TicketGraphLimits.MaxDoneItem) return Over(t, "done item", item.Length, TicketGraphLimits.MaxDoneItem);
        }
        return null;
    }

    private static string Over(GraphTicket t, string field, int length, int bound) =>
        $"ticket '{t.Key}': the '{field}' is {length} characters and its bound is {bound} — shorten it";

    private static string? Requires(GraphTicket t, IReadOnlySet<string> siblings)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var required in t.Requires)
        {
            var existing = GraphTicketRequire.ExistingId(required);
            if (existing is { Length: 0 })
                return $"ticket '{t.Key}' requires '{required}' with no id after the prefix — write ticket:<id> of a filed ticket";
            if (existing is null && required == t.Key) return $"ticket '{t.Key}' requires itself";
            if (existing is null && !siblings.Contains(required))
                return $"ticket '{t.Key}' requires '{required}', which is no ticket of this list — name a sibling's key "
                    + "or ticket:<id> of a ticket already filed";
            if (!seen.Add(existing is null ? required : GraphTicketRequire.ExistingPrefix + existing))
                return $"ticket '{t.Key}' requires '{required}' twice — name each requirement once";
        }
        return null;
    }

    private static string? Repos(GraphTicket t, IReadOnlyList<string> scope)
    {
        if (scope.Count == 0) return null;
        var outside = t.Repos.FirstOrDefault(r => !scope.Contains(r, StringComparer.OrdinalIgnoreCase));
        return outside is null
            ? null
            : $"ticket '{t.Key}' names the repository '{outside}', which this conversation's scope does not hold — "
                + $"name one of {string.Join(", ", scope)}";
    }
}
