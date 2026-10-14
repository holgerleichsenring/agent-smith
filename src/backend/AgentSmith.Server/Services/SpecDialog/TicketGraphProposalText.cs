using System.Text;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-10-14-dbcek: a proposed list of tickets as a chat channel reads it — COMPACT, because twenty ticket texts do
/// not fit a chat message: one line per ticket in the order they must merge, with its key, title, number of done
/// items and what it waits for, and a pointer to the dashboard's proposal pane for the full texts. An existing ticket
/// reads #&lt;id&gt; when its id is numeric and as written otherwise (a Jira key): the text has no tracker type.
/// </summary>
public static class TicketGraphProposalText
{
    public static string Describe(TicketGraphOutcome graph, SpecDialogMarkup m)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(m);
        var ordered = new TicketGraphOrderer().Order(graph.Tickets).Tickets;
        var sb = new StringBuilder();
        sb.AppendLine($"Proposed outcome: {m.Bold($"{ordered.Count} tickets")}, in the order they must merge");
        for (var i = 0; i < ordered.Count; i++)
        {
            var ticket = ordered[i];
            var done = ticket.Done.Count == 1 ? "1 done item" : $"{ticket.Done.Count} done items";
            sb.AppendLine($"{i + 1}. `{ticket.Key}` {ticket.Title} — {done}; {WaitsFor(ticket)}");
        }
        sb.Append("The full ticket texts are on the dashboard's proposal pane.");
        return sb.ToString();
    }

    private static string WaitsFor(GraphTicket ticket) => ticket.Requires.Count == 0
        ? "nothing to wait for"
        : "waits for " + string.Join(", ", ticket.Requires.Select(Named));

    private static string Named(string requirement) => GraphTicketRequire.ExistingId(requirement) is { } id
        ? id.Length > 0 && id.All(char.IsAsciiDigit) ? $"#{id}" : id
        : requirement;
}
