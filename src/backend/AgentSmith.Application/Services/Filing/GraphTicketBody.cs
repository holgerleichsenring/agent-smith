using System.Text;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Tickets;

namespace AgentSmith.Application.Services.Filing;

/// <summary>
/// 2026-10-14-dbcel: the body a list's ticket is filed with. 2026-11-17-dd05b: its specs' region — the requirement and
/// criteria as the approved specs say them, rendered by the filing's renderer — then what must merge first. Never the
/// list's free text: the specs on its branch are the ticket's truth, and its body says what they say.
/// </summary>
internal static class GraphTicketBody
{
    internal const string RequiresHeading = "## Requires";

    internal const string RequiresLine = "Merge these first; this ticket starts once each is merged or marked done by hand.";

    /// <param name="region">The specs' region, as <see cref="SeriesTicketRegion"/> renders it.</param>
    /// <param name="required">Each required ticket's tracker key, with its title when it is a sibling of this list.</param>
    internal static SpecTicketContent Render(
        GraphTicket ticket, SpecTicketContent region, IReadOnlyList<(string Key, string? Title)> required)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        ArgumentNullException.ThrowIfNull(region);
        ArgumentNullException.ThrowIfNull(required);
        var body = new StringBuilder(region.Body.TrimEnd()).AppendLine();
        if (required.Count > 0)
        {
            body.AppendLine().AppendLine(RequiresHeading).AppendLine(RequiresLine);
            foreach (var (key, title) in required) body.AppendLine(title is null ? $"- {key}" : $"- {key} — {title}");
        }
        return new SpecTicketContent(TicketTitle.Fit(ticket.Title), body.ToString());
    }
}
