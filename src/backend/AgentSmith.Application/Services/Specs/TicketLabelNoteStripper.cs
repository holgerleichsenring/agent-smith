using System.Text.RegularExpressions;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-18-d518: removes <see cref="TicketLabelNote"/> from ticket-origin text at the DOOR —
/// the ticket fetch — rather than at the readers, because the readers are more than anyone
/// counts: the master prompt, the scope classifier, the derivation, its acceptance-criteria
/// read, the epic-ground section and the CUT REVIEW, which takes the ticket text with no fence
/// at all. Worse, the whole-ticket fallback pins the entire description AS the ratified phase
/// markdown, where a leak is not a prompt hazard but the specification. A per-reader stripper is
/// a list that goes stale; the fetch handler is a door that cannot.
/// <para>
/// Read in all THREE encodings a description arrives in: the markdown two trackers store, the
/// HTML Azure DevOps converts it to on create and hands back on read, and the structured
/// document Jira stores line by line — the marker's literal characters round-trip through all of
/// them, and a markdown-only stripper would leak the note straight into the unfenced cut review.
/// </para>
/// <para>
/// Only <see cref="TicketLabelNote.BeginIdentifier"/> is matched, with ANY text tolerated
/// between it and the comment's close, because the begin marker's explaining sentence is
/// editable on Jira. A HALF-DELETED note — one marker without its partner — is left alone as
/// ordinary ticket prose: it then reaches every reader and changes the ticket fingerprint, and
/// both are accepted, because the alternative is a heuristic over operator text.
/// </para>
/// </summary>
public static partial class TicketLabelNoteStripper
{
    // Non-greedy to `-->` under Singleline rather than `[^>]*`: the display sentence is prose,
    // and a future rewording of it could legitimately contain a `>`.
    private const string NotePattern =
        @"<!--\s*" + TicketLabelNote.BeginIdentifier + @".*?-->.*?<!--\s*"
        + TicketLabelNote.EndIdentifier + @"\s*-->";

    /// <summary>
    /// The ticket as every reader downstream sees it: its own text, without our note. A ticket
    /// that carries none is passed through AS IT CAME — the door copies nothing it did not change.
    /// </summary>
    public static Ticket Strip(Ticket ticket)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        var description = Strip(ticket.Description);
        return ReferenceEquals(description, ticket.Description)
            ? ticket
            : ticket.WithDescription(description);
    }

    /// <summary>
    /// The description without the note, or the text exactly as it arrived when it carries no
    /// complete marker pair.
    /// </summary>
    public static string Strip(string? description) =>
        string.IsNullOrEmpty(description) || !Note().IsMatch(description)
            ? description ?? string.Empty
            : Note().Replace(description, string.Empty).TrimEnd();

    [GeneratedRegex(NotePattern, RegexOptions.Singleline)]
    private static partial Regex Note();
}
