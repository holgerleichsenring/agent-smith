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
}
