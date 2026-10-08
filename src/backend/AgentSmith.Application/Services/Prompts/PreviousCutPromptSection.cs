using System.Text;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Prompts;

/// <summary>
/// p0393a: renders the previous cut for a re-entry — every phase with whether it already
/// executed, and the cause of the revision being written — so the derivation AMENDS
/// the set instead of re-deriving it from the prose. A previous set that was a
/// hand-back holds no cut to amend and renders nothing: the hand-back itself reaches
/// the prompt through the conversation section, or the unanswered-question pin.
/// <para>
/// 2026-09-08-5cd2: an edited ticket says so, naming the revision the change postdates —
/// the unstarted phases are cut from the current text, the executed ones stay.
/// 2026-09-08-4aa9: a comment says so the same way, pointing at the conversation section
/// the comment is in.
/// </para>
/// </summary>
public static class PreviousCutPromptSection
{
    public static string Render(SpecSet? previous, string cause)
    {
        if (previous is null || previous.IsHandedBack) return string.Empty;
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("## The previous cut — AMEND it, do not re-derive from the prose");
        sb.AppendLine($"Cause of the revision you are writing now: {cause}");
        if (cause == SpecRevisionCause.TicketEdit) sb.AppendLine(TicketEditParagraph(previous));
        if (cause == SpecRevisionCause.Comment) sb.AppendLine(CommentParagraph(previous));
        if (cause == SpecRevisionCause.Rework) sb.AppendLine(ReworkParagraph(previous));
        if (cause == SpecRevisionCause.StatusBack) sb.AppendLine(StatusBackParagraph(previous));
        if (SpecAmendmentRule.IsAppend(cause, previous)) sb.AppendLine(AppendParagraph(previous, cause));
        foreach (var phase in previous.Phases)
        {
            var executed = previous.Executed.Contains(phase.PhaseId, StringComparer.Ordinal);
            sb.AppendLine(
                $"- {phase.PhaseId} ({(executed ? "EXECUTED — keep exactly as it is" : "not started")}): "
                + phase.Draft.Goal);
        }
        sb.AppendLine();
        sb.AppendLine(
            "Return the WHOLE set again. Repeat every executed phase unchanged — same goal, "
            + "same done-list, same order. You may merge, split or reorder only the phases "
            + "that have not started.");
        return sb.ToString();
    }

    private static string TicketEditParagraph(SpecSet previous) =>
        $"The ticket text changed since revision {previous.Current.Number} was cut. The segments "
        + "above are the CURRENT text: cut the phases that have not started from it — drop what "
        + "the ticket no longer asks for, add what it now asks for, keep what still holds. "
        + "Executed phases stay exactly as they are.";

    private static string ReworkParagraph(SpecSet previous) =>
        $"A reviewer requested changes on the pull request after revision {previous.Current.Number} was cut — "
        + "the review is in the pull request review section above. Amend the phases that have not started as it "
        + "asks. Executed phases stay exactly as they are.";

    // 2026-10-08-f114: with nothing left to run, the only place the input can go is AFTER the head.
    private static string AppendParagraph(SpecSet previous, string cause) =>
        $"Every phase above has executed. Repeat them unchanged and ADD new phases after them that carry out "
        + $"what {Source(cause)} "
        + $"asks — at most {SpecSet.MaxPhases - previous.Executed.Count} new phase(s).";

    private static string Source(string cause) => cause switch
    {
        SpecRevisionCause.Rework => "the pull request review section",
        SpecRevisionCause.StatusBack => "the ticket conversation, the current ticket text and the pull request review section",
        _ => "the ticket conversation and the current ticket text",
    };

    // 2026-10-08-2123: a person moved the finished ticket back — the feedback is wherever they wrote it.
    private static string StatusBackParagraph(SpecSet previous) =>
        $"A person moved the ticket back to work after revision {previous.Current.Number} ran — what they want is in the "
        + "ticket conversation, the current ticket text and the pull request review section above.";

    private static string CommentParagraph(SpecSet previous) =>
        $"The ticket was commented on after revision {previous.Current.Number} was cut — the comment "
        + "is in the ticket conversation above. Amend the phases that have not started as it asks: "
        + "re-cut, drop or add them. Executed phases stay exactly as they are.";
}
