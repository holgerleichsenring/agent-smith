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
/// <para>
/// 2026-09-27-481ba: THE THREAD IS ORDERED HERE, and the cap falls between comments. Nothing
/// upstream sorts — not one of the four providers and not one of the four comment mappers, and
/// Azure DevOps is asked with no sort order at all — so the order used to be whatever a tracker
/// happened to return, and a raw prefix cut took whichever end that put last, mid-word. The head
/// is kept first because it is what the ticket IS; the comments that follow are the newest that
/// fit, whole; and the one remaining character cut is a head that alone exceeds the cap.
/// </para>
/// </summary>
public static class TicketTextComposer
{
    public static ComposedTicketText Compose(
        Ticket ticket, IReadOnlyList<TicketComment>? comments)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        var head = Head(ticket);
        if (head.Length >= SeededTicketLimits.Text)
            return new ComposedTicketText(head[..SeededTicketLimits.Text], Truncated: true);

        var ordered = (comments ?? [])
            .Where(c => !OwnTicketComment.IsOurs(c))
            .OrderBy(c => c.CreatedAt)
            .Select(c => $"{c.Author}: {c.Body}")
            .ToList();
        // Two newlines join the comments and separate them from the head, so the budget the fit is
        // given is what is left after both.
        var separators = ordered.Count == 0 ? 0 : 2 * ordered.Count;
        var fitted = NewestFirstFit.Of(
            ordered, SeededTicketLimits.Text - head.Length - separators, mayOvershoot: false);

        var whole = fitted.Kept.Count == 0
            ? head
            : head + "\n\n" + string.Join("\n\n", fitted.Kept);
        return new ComposedTicketText(
            whole.TrimEnd(), Truncated: fitted.Dropped > 0 || fitted.NewestCut);
    }

    /// <summary>
    /// 2026-09-27-481ba: the same composition with NO cap — what a read of the whole ticket
    /// answers from. One composition, so what the model reads in slices is what it would have been
    /// seeded with had the cap not bitten.
    /// </summary>
    public static string Whole(Ticket ticket, IReadOnlyList<TicketComment>? comments)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        var ordered = (comments ?? [])
            .Where(c => !OwnTicketComment.IsOurs(c))
            .OrderBy(c => c.CreatedAt)
            .Select(c => $"{c.Author}: {c.Body}");
        return string.Join("\n\n", new[] { Head(ticket) }.Concat(ordered)).TrimEnd();
    }

    private static string Head(Ticket ticket)
    {
        var body = new System.Text.StringBuilder();
        body.Append("Title: ").AppendLine(ticket.Title);
        // The note lives between markers in the description; the stripper is the run path's own.
        body.AppendLine().AppendLine(TicketLabelNoteStripper.Strip(ticket).Description);
        if (!string.IsNullOrWhiteSpace(ticket.AcceptanceCriteria))
            body.AppendLine().AppendLine("Acceptance criteria:").AppendLine(ticket.AcceptanceCriteria);
        return body.ToString().TrimEnd();
    }
}

/// <summary>The text, and whether the cap dropped part of the ticket.</summary>
public sealed record ComposedTicketText(string Text, bool Truncated);
