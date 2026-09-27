using AgentSmith.Application.Services.Prompts;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Contracts.Tickets;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-09-25-8e51c: the ticket text a design conversation is grounded on — title, body,
/// acceptance criteria and the comments that are not ours — stripped of the framework's own note
/// and CAPPED.
/// <para>
/// The cap is the phase's load-bearing number. A conversation re-sends its whole prompt every
/// turn, on the one surface with neither a cost fence nor an iteration ceiling, so whatever is
/// seeded is multiplied by the length of the conversation rather than paid once — the same
/// reasoning that made the image ceiling four rather than ten. One live ticket thread ran to
/// 147,462 characters.
/// </para>
/// <para>
/// OUR OWN COMMENTS ARE DROPPED. A filed ticket carries this framework's notices, and feeding
/// them back shows the model its own echo — the thing the run path's conversation section already
/// filters out by the same predicate.
/// </para>
/// </summary>
public static class TicketTextComposer
{
    public static ComposedTicketText Compose(
        Ticket ticket, IReadOnlyList<TicketComment>? comments)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        var body = new System.Text.StringBuilder();
        body.Append("Title: ").AppendLine(ticket.Title);
        // The note lives between markers in the description; the stripper is the run path's own.
        body.AppendLine().AppendLine(TicketLabelNoteStripper.Strip(ticket).Description);
        if (!string.IsNullOrWhiteSpace(ticket.AcceptanceCriteria))
            body.AppendLine().AppendLine("Acceptance criteria:").AppendLine(ticket.AcceptanceCriteria);
        foreach (var comment in comments ?? [])
        {
            if (OwnTicketComment.IsOurs(comment)) continue;
            body.AppendLine().Append(comment.Author).Append(": ").AppendLine(comment.Body);
        }

        var whole = body.ToString().TrimEnd();
        return whole.Length <= SeededTicketLimits.Text
            ? new ComposedTicketText(whole, Truncated: false)
            : new ComposedTicketText(whole[..SeededTicketLimits.Text], Truncated: true);
    }
}

/// <summary>The text, and whether the cap dropped part of the ticket.</summary>
public sealed record ComposedTicketText(string Text, bool Truncated);
