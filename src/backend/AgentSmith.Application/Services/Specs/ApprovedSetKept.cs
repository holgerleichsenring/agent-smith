using System.Text;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-17-0e79b: what a run says when an input arrived for a set a person APPROVED and the
/// run kept the set anyway.
/// <para>
/// The cause constants keep their exact values for a set nobody approved, where the same causes
/// still mean the model ran; the "was kept" wording is added where the revision header is built,
/// the one place that knows both the cause and the set. The same sentence is the notice posted to
/// the ticket and recorded as a run decision, so the two cannot drift apart.
/// </para>
/// <para>
/// A KEPT EDIT MUST CLEAR ITS CAUSE, or it fires forever: the fingerprint is refreshed only when
/// the model ran, and an approved set never runs it, so the edited text would differ from the
/// stored fingerprint on every later run and re-fire the cause, the revision and the notice. The
/// COMMENT cause clears itself — the cut comment is posted on every run and moves the anchor the
/// comment rule measures from.
/// </para>
/// <para>
/// A cause names ONE input, and an edit outranks a comment. Both are reported: neither was acted
/// on, and telling an operator about the edit while silently dropping their comment is the defect
/// this notice exists to prevent. The DISCARDED edit — a re-approval's change to a phase that
/// already ran — is reported here too, for the same reason.
/// </para>
/// </summary>
public static class ApprovedSetKept
{
    /// <summary>The phrase the revision cause and the notice both end on.</summary>
    public const string Kept = "the approved set was kept";

    /// <summary>
    /// Marks the notice as this system's own, so the next run does not read it as somebody
    /// answering (<see cref="Prompts.OwnTicketComment"/>). It deliberately does NOT carry
    /// <see cref="SpecSetComment.CutMarker"/>: that phrase is the anchor the comment rule measures
    /// from, and a second comment carrying it would move the anchor past the operator's own.
    /// </summary>
    public const string Heading = "## Agent Smith — the approved specification stands";

    /// <summary>Where a change to an approved set is actually made TODAY. Not the design
    /// conversation: nothing re-opens an approved record from one yet, and a wording that sent an
    /// operator there would be the promise this phase deleted, pointing somewhere else.</summary>
    public const string WhereToChangeIt = OnTheBranch + " and open in the pull request this run "
        + "linked. " + TheEditRule;

    /// <summary>2026-09-17-0e79c: the same place, for a run that has NOT opened a pull request
    /// yet. A hand-back on the FIRST phase of a set happens before any delivery, so naming a
    /// pull request there would send an operator to something that does not exist.</summary>
    public const string WhereToChangeItWithNoPullRequest = OnTheBranch + ". " + TheEditRule;

    /// <summary>2026-09-22-8b25: the ONE deliberate door for somebody who cannot reach the branch,
    /// named where a person learns their input was not acted on. The phrase sits INSIDE a sentence
    /// and never on a line of its own, so quoting this notice back cannot fire a demand:
    /// <see cref="SpecRecutDemand"/> reads it only as a whole line at the START of a comment.</summary>
    public const string TheDoor =
        "If you cannot reach the branch at all, there is one deliberate exception: write a comment "
        + "whose FIRST line says nothing but `" + SpecSetComment.RecutDemand + "`, and the next "
        + "run cuts the unstarted phases again from the ticket. Any other first line is an "
        + "ordinary comment and changes nothing.";

    private const string OnTheBranch = "The specs are on the ticket branch under `.agentsmith/specs/`";

    private const string TheEditRule =
        "Edit a phase that has NOT started there and the next run works your edit; a phase that "
        + "already ran is never edited, so a correction to one becomes a new phase.";

    // The input this run saw and kept, or null when nothing arrived that would have re-cut a set
    // nobody approved. Not public: the three questions callers actually ask are below.
    private static string? InputOf(SpecSet? set, string? cause) => set?.Approval is null ? null : cause switch
    {
        SpecRevisionCause.Comment => "a comment arrived on the ticket",
        SpecRevisionCause.TicketEdit => "the ticket text was edited",
        _ => null,
    };

    /// <summary>The cause the revision names — the real cause, plus that the set was kept.</summary>
    public static string CauseFor(SpecSet? set, string cause) =>
        InputOf(set, cause) is null ? cause : $"{cause} — {Kept}";

    /// <summary>True when the run SAW a ticket edit and kept the set, so the fingerprint it
    /// publishes must be the text it saw — once the edit has actually been reported.</summary>
    public static bool SawAnEdit(SpecSet? set, string cause) =>
        set?.Approval is not null && string.Equals(cause, SpecRevisionCause.TicketEdit, StringComparison.Ordinal);

    /// <summary>
    /// The notice, or null when there is nothing to report. <paramref name="discarded"/> is the
    /// merge's note about an edit to a phase that already ran; <paramref name="alsoCommented"/>
    /// names a comment the cause could not, because an edit outranked it.
    /// </summary>
    public static string? Notice(
        SpecSet? set, string cause, string? discarded = null, bool alsoCommented = false)
    {
        var arrived = Arrived(set, cause, alsoCommented);
        if (arrived is null && discarded is null) return null;
        var sb = new StringBuilder().AppendLine(Heading).AppendLine();
        if (arrived is not null)
            sb.AppendLine($"{Capitalise(arrived)}, and {Kept}: it was approved in design "
                + $"conversation {Conversation(set!)}, and this run works it unchanged.").AppendLine();
        if (discarded is not null)
            sb.AppendLine($"One change was discarded — {discarded}.").AppendLine();
        return sb.AppendLine(WhereToChangeIt).AppendLine().Append(TheDoor).ToString();
    }

    private static string? Arrived(SpecSet? set, string cause, bool alsoCommented)
    {
        var input = InputOf(set, cause);
        if (input is null) return null;
        return alsoCommented && string.Equals(cause, SpecRevisionCause.TicketEdit, StringComparison.Ordinal)
            ? $"{input}, and a comment arrived on the ticket"
            : input;
    }

    private static string Conversation(SpecSet set) =>
        string.IsNullOrWhiteSpace(set.Approval?.Conversation) ? "(unnamed)" : set.Approval!.Conversation;

    private static string Capitalise(string text) => char.ToUpperInvariant(text[0]) + text[1..];
}
