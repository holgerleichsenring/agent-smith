namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-09-18-d518: the short note a filed ticket carries about what its LABELS bind — the one
/// thing on a framework-filed ticket that a person cannot read off the body, because the labels
/// route the ticket and guard its specification from somewhere the body never mentions.
/// <para>
/// It is wrapped in a MARKER PAIR rather than recognised by its heading: the heading breaks the
/// moment an operator edits the note's first line, and the note's inner content changes shape
/// across the conversions a description makes on the way back (a heading becomes a heading
/// element with a generated identifier), while a marker pair is indifferent to everything
/// between the markers. <see cref="Specs.TicketLabelNoteStripper"/> reads this contract from the
/// other side and removes the note at the ticket-fetch door, so no model prompt and no pull
/// request body ever carries it.
/// </para>
/// <para>
/// Only <see cref="BeginIdentifier"/> is the contract; the sentence after it is DISPLAY TEXT. On
/// Jira the description is stored as a structured document that renders HTML as literal text, so
/// an operator sees the delimiters and may reword that sentence — matching the whole literal
/// would make it an editable first line, and an edit would break the pair. One constraint on
/// rewording it: an HTML comment may not contain a double hyphen, so the separator stays a single
/// one or becomes a dash character.
/// </para>
/// </summary>
public static class TicketLabelNote
{
    /// <summary>The stable identifier the stripper matches. Never reworded.</summary>
    public const string BeginIdentifier = "agentsmith:note:begin";

    /// <inheritdoc cref="BeginIdentifier"/>
    public const string EndIdentifier = "agentsmith:note:end";

    /// <summary>The begin marker EXPLAINS ITSELF, because on one tracker a person reads it.</summary>
    public const string Begin = "<!-- " + BeginIdentifier
        + " - framework bookkeeping, removed before any model or pull request reads this ticket -->";

    /// <inheritdoc cref="Begin"/>
    public const string End = "<!-- " + EndIdentifier + " -->";

    /// <summary>
    /// Bare prose under a heading, never a list: the acceptance-criteria scan breaks on any
    /// heading and takes only marker lines, so a heading ends the scan and prose cannot be
    /// mistaken for a criterion — a bulleted note under the same heading could be.
    /// </summary>
    public const string Heading = "## What these labels bind";

    /// <summary>
    /// 2026-09-25-c1f7: A HINT, NOT A WARNING. The note warned what removing the label costs
    /// because the label was load-bearing twice over: 2026-09-25-3c7aa moved the ROUTING bind onto
    /// the server's approval record, and this phase moved DISCOVERY onto it as well — the poll
    /// names an approved ticket by its id beside the label guard on the two trackers that filter
    /// server-side, and the two that do not never read the guard at all. So the sentence now says
    /// what the label is FOR, which is a person reading the board.
    /// <para>
    /// It stops short of "removing it is free". The approval record is what binds, and the note
    /// says so; whether one particular deletion changes nothing also depends on how many
    /// approvals that tracker has open at once, which is not a fact a ticket can carry.
    /// </para>
    /// </summary>
    private static string StampSentence(string stamp) =>
        "The `" + stamp + "` label marks this ticket as one an approved specification exists for, "
        + "so a person scanning the board can see which tickets those are. It is a hint, not the "
        + "binding: the approval the server recorded is what binds this ticket to phase execution, "
        + "and it is what the poll and the routing both read.";

    /// <summary>
    /// The note for the labels a ticket is actually filed with, or null when it carries none the
    /// framework can explain — a bug is filed with an empty label set, and a note about labels it
    /// does not carry would be false.
    /// </summary>
    /// <param name="approvedSetStamp">2026-09-25-3c7ac: what THIS board calls the stamp. The note
    /// has to name the word the ticket actually carries, or it explains a label nobody can see.</param>
    public static string? For(IReadOnlyCollection<string> labels, string? approvedSetStamp = null)
    {
        ArgumentNullException.ThrowIfNull(labels);
        var stamp = approvedSetStamp ?? FiledTicketLabels.ApprovedSetStamp;
        List<string> sentences = [];
        if (Carries(labels, stamp)) sentences.Add(StampSentence(stamp));
        return sentences.Count == 0
            ? null
            : $"{Begin}\n{Heading}\n{string.Join("\n\n", sentences)}\n{End}\n";
    }

    private static bool Carries(IEnumerable<string> labels, string label) =>
        labels.Any(l => string.Equals(l, label, StringComparison.OrdinalIgnoreCase));
}
