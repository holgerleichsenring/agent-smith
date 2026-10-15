using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-10-14-dbcej: parses a `kind: tickets` outcome block — a list of ticket texts, each a mapping
/// of key, title, requirement, done, requires and repos — and hands it to <see cref="TicketGraphChecker"/>.
/// <para>
/// The block is read untyped, so an unquoted ': ' inside a list item becomes a mapping and a single
/// value written where a list belongs reads as a scalar. Both are refused naming the ticket and the
/// field, never read as empty: a <c>requires: order-api</c> read as nothing would file the ticket as a
/// root that starts before what it needs has merged. Only an absent or null list is empty. Every
/// refusal is an <see cref="OutcomeInvalid"/>, so the turn is re-prompted, never failed.
/// </para>
/// </summary>
public sealed class TicketGraphOutcomeParser(TicketGraphChecker checker)
{
    /// <param name="scope">The turn's repository names each ticket's repos is checked against.</param>
    public OutcomeResolution Parse(IReadOnlyDictionary<string, object?> map, IReadOnlyList<string> scope)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(scope);
        IReadOnlyList<object?> items = [];
        if (map.TryGetValue("tickets", out var node) && node is not null)
        {
            if (node is not List<object?> list)
                return new OutcomeInvalid("the tickets outcome: 'tickets' is not a list — write it as a list of ticket mappings");
            items = list;
        }

        var tickets = new List<GraphTicket>(items.Count);
        for (var i = 0; i < items.Count; i++)
        {
            var (ticket, error) = Read(items[i], i + 1);
            if (ticket is null) return new OutcomeInvalid(error!);
            tickets.Add(ticket);
        }
        var graph = new TicketGraphOutcome(tickets);
        return checker.Check(graph, scope) is { } reason ? new OutcomeInvalid(reason) : new OutcomeResolved(graph);
    }

    private static (GraphTicket? Ticket, string? Error) Read(object? node, int position)
    {
        if (node is not Dictionary<object, object?> raw)
            return (null, $"ticket #{position} is not a mapping — write each ticket with key, title, requirement, done, "
                + "requires and repos");
        var fields = raw.ToDictionary(e => e.Key.ToString() ?? string.Empty, e => e.Value);
        var name = fields.TryGetValue("key", out var keyNode) && keyNode is string stated && stated.Trim().Length > 0 ? $"'{stated.Trim()}'" : $"#{position}";
        if (fields.Keys.FirstOrDefault(f => !Known(f)) is { } unknown)
            return (null, $"ticket {name} carries the unknown field '{unknown}' — a ticket has key, title, requirement, "
                + "done, requires and repos");

        string? error = null;
        string Text(string field)
        {
            if (!fields.TryGetValue(field, out var value) || value is null) return string.Empty;
            if (value is string text) return text.Trim();
            error ??= $"ticket {name}: '{field}' is not a string — quote the item";
            return string.Empty;
        }
        IReadOnlyList<string> Items(string field)
        {
            if (!fields.TryGetValue(field, out var value) || value is null) return [];
            if (value is not List<object?> list)
            {
                error ??= $"ticket {name}: '{field}' is not a list — write it as a list: [{(value as string)?.Trim() ?? "..."}]";
                return [];
            }
            if (list.Any(item => item is not string))
            {
                error ??= $"ticket {name}: an item of '{field}' is not a string (an unquoted ': ' makes it a mapping) — quote the item";
                return [];
            }
            return [.. list.Cast<string>().Select(item => item.Trim())];
        }

        var ticket = new GraphTicket(
            Text("key"), Text("title"), Text("requirement"), Items("done"), Items("requires"), Items("repos"));
        return error is null ? (ticket, null) : (null, error);
    }

    private static bool Known(string field) =>
        field is "key" or "title" or "requirement" or "done" or "requires" or "repos";
}
