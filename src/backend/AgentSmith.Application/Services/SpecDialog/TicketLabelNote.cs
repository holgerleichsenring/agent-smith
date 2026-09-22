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
    /// 2026-09-22-766b: ONE SENTENCE, because one label now does both jobs — it records that an
    /// approved specification exists and it is what routes the ticket. What removal COSTS is
    /// named, never that it is harmless: the guard fails open, and the routing half is the
    /// MECHANISM rather than an outcome — with pipeline-from-label the resolver hard-binds on
    /// the stamp; without it this project's own map may match, or a declared default pipeline
    /// answers, or nothing does and the ticket is dropped. The sentence is true on all of them.
    /// </summary>
    private const string StampSentence =
        "The `" + FiledTicketLabels.ApprovedSetStamp + "` label says that an approved "
        + "specification exists for this ticket, and it is what binds this ticket to phase "
        + "execution. Removing it costs the ticket its one guard against a lost hand-off being "
        + "re-derived from a description anyone can edit, and leaves it routed by this project's "
        + "own rules, or dropped.";

    /// <summary>
    /// The note for the labels a ticket is actually filed with, or null when it carries none the
    /// framework can explain — a bug is filed with an empty label set, and a note about labels it
    /// does not carry would be false.
    /// </summary>
    public static string? For(IReadOnlyCollection<string> labels)
    {
        ArgumentNullException.ThrowIfNull(labels);
        List<string> sentences = [];
        if (Carries(labels, FiledTicketLabels.ApprovedSetStamp)) sentences.Add(StampSentence);
        return sentences.Count == 0
            ? null
            : $"{Begin}\n{Heading}\n{string.Join("\n\n", sentences)}\n{End}\n";
    }

    private static bool Carries(IEnumerable<string> labels, string label) =>
        labels.Any(l => string.Equals(l, label, StringComparison.OrdinalIgnoreCase));
}
