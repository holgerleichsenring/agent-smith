using System.Text;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Tickets;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-10-14-dbcel: the body a list's ticket is filed with — its TEXT, no spec: the requirement, the done items as
/// acceptance criteria (list items, the only lines read back as criteria), what must merge first, and the conversation
/// it came from. The run that starts it derives its specs against the code merged by then.
/// </summary>
internal static class GraphTicketBody
{
    internal const string RequiresHeading = "## Requires";

    internal const string RequiresLine = "Merge these first; this ticket starts once each is merged or marked done by hand.";

    /// <param name="required">Each required ticket's tracker key, with its title when it is a sibling of this list.</param>
    /// <param name="labelNote">The note on the labels the ticket is filed with, or null for none.</param>
    internal static SpecTicketContent Render(
        GraphTicket ticket, IReadOnlyList<(string Key, string? Title)> required, string conversation, string? labelNote)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        ArgumentNullException.ThrowIfNull(required);
        var body = new StringBuilder(ticket.Requirement.Trim()).AppendLine().AppendLine();
        if (ticket.Done.Count > 0)
        {
            body.AppendLine(AcceptanceCriteriaSection.Heading);
            foreach (var done in ticket.Done) body.AppendLine($"- {CriterionLine.Collapse(done)}");
            body.AppendLine();
        }
        if (required.Count > 0)
        {
            body.AppendLine(RequiresHeading).AppendLine(RequiresLine);
            foreach (var (key, title) in required) body.AppendLine(title is null ? $"- {key}" : $"- {key} — {title}");
            body.AppendLine();
        }
        body.AppendLine($"Filed from design conversation `{conversation}`; its specs are derived by the run that starts it.");
        if (labelNote is not null) body.AppendLine().Append(labelNote);
        return new SpecTicketContent(TicketTitle.Fit(ticket.Title), body.ToString());
    }
}
