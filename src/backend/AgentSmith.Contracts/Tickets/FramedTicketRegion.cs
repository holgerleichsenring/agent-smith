using System.Text.RegularExpressions;

namespace AgentSmith.Contracts.Tickets;

/// <summary>
/// 2026-09-25-8e51e: the region of a ticket body the FRAMEWORK owns, marked so that it can be
/// replaced without touching a word anybody else wrote.
/// <para>
/// A MARKER PAIR rather than a heading, for the reason <c>TicketLabelNote</c> gives: a heading
/// breaks the moment an operator edits the first line under it, and the inner content changes
/// shape across the conversions a description makes on the way back — Azure DevOps hands back
/// HTML, Jira a structured document — while a pair is indifferent to everything between its
/// markers. Only <see cref="BeginIdentifier"/> is the contract; the sentence after it is display
/// text an operator may reword, which is why the pattern tolerates any text up to the close.
/// </para>
/// <para>
/// A body with NO complete pair is a ticket filed before this spec, and
/// <see cref="Replace"/> answers null for it rather than guessing where the framework's text
/// ends. Appending a second rendering would leave the ticket saying two things; overwriting the
/// whole body would delete prose nobody can attribute. The caller refuses and says so.
/// </para>
/// <para>
/// 2026-10-28-7a69b: unless the caller knows the framework never rendered it — a ticket with no
/// approval record is a person's text, and the region goes below it.
/// </para>
/// </summary>
public static partial class FramedTicketRegion
{
    /// <summary>The stable identifier a rewrite matches. Never reworded.</summary>
    public const string BeginIdentifier = "agentsmith:spec:begin";

    /// <inheritdoc cref="BeginIdentifier"/>
    public const string EndIdentifier = "agentsmith:spec:end";

    /// <summary>The begin marker EXPLAINS ITSELF, because on Jira a person reads it literally.</summary>
    public const string Begin = "<!-- " + BeginIdentifier
        + " - written from the approved specification; edits between these markers are replaced "
        + "when it is amended. Your own text belongs outside them. -->";

    /// <inheritdoc cref="Begin"/>
    public const string End = "<!-- " + EndIdentifier + " -->";

    /// <summary>The framework's own rendering, marked as its own.</summary>
    public static string Wrap(string body) => $"{Begin}\n\n{body?.TrimEnd()}\n\n{End}\n";

    /// <summary>
    /// The body with the framework's region replaced by <paramref name="region"/>, or null when
    /// the body carries no complete marker pair and may not take one appended.
    /// </summary>
    /// <param name="region">A whole rendering, markers included, as <see cref="Wrap"/> makes one.</param>
    /// <param name="placement">2026-10-28-7a69b: whether an unmarked body takes the region below it.</param>
    public static string? Replace(string? body, string region, RegionPlacement placement = RegionPlacement.ReplaceOnly)
    {
        if (Marked(body)) return Pair().Replace(body!, region.Replace("$", "$$", StringComparison.Ordinal), 1);
        if (placement != RegionPlacement.ReplaceOrAppend || Damaged(body)) return null;
        return string.IsNullOrWhiteSpace(body) ? region : $"{body.TrimEnd()}\n\n{region}";
    }

    // A marker without its partner: the framework's text is in there somewhere, so a second region would duplicate it.
    private static bool Damaged(string? body) => body is not null
        && (body.Contains(BeginIdentifier, StringComparison.Ordinal) || body.Contains(EndIdentifier, StringComparison.Ordinal));

    /// <summary>Whether this body carries a complete framework region.</summary>
    public static bool Marked(string? body) => !string.IsNullOrEmpty(body) && Pair().IsMatch(body);

    // Non-greedy to the close for the reason the label note's stripper is: the display sentence
    // is prose, and a rewording of it could legitimately contain a '>'.
    [GeneratedRegex(
        @"<!--\s*" + BeginIdentifier + @".*?-->.*?<!--\s*" + EndIdentifier + @"\s*-->",
        RegexOptions.Singleline)]
    private static partial Regex Pair();
}
