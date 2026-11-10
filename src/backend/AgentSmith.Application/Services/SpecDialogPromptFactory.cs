using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services;

/// <summary>
/// p0315b: renders the spec-dialog transcript into the design-partner
/// master's user prompt. The reply contract is stated here (answer the last
/// user turn; the reply text is delivered verbatim to the chat thread) so the
/// SKILL.md stays about behaviour, not plumbing.
/// <para>
/// 2026-09-22-deda: the contract also says what a conversation that has already filed work
/// cannot do to it. A capability, not a state: it names no ticket and claims nothing about a
/// tracker, so it cannot go stale between the filing and the turn that reads it.
/// </para>
/// </summary>
public sealed class SpecDialogPromptFactory : ISpecDialogPromptFactory
{
    public string Build(PipelineContext pipeline, int imagesExisting, int imagesCarried)
    {
        var transcript = DialogTranscriptSection.Of(pipeline);

        return $"""
            ## Design conversation
            The transcript of this design thread so far, oldest first. Respond to the
            LAST user turn; earlier turns are context you already produced or received.

            {DialogTranscriptSection.Render(pipeline, transcript, stewarded: false)}
            {SpecDialog.SeededTicketSection.Render(pipeline)}
            {SpecDialog.FrameworkFactsSection.Render(pipeline)}
            {SpecDialog.DialogScopeRepositoriesSection.Render(pipeline)}
            {SpecDialogRevisionSection.Render(pipeline)}

            ## Your reply
            Write the reply text now — it is delivered verbatim to the chat thread.
            {DialogReadingLine.Render(pipeline)}
            {DialogImageNote.Render(imagesExisting, imagesCarried)}
            {ConversationNameNote.Render(pipeline)}
            {WorkPictureSection.Render(pipeline)}
            {TurnContract(transcript)}
            """;
    }

    // 2026-09-22-deda: one contract, composed per turn from the transcript the turn began
    // with — what it may propose, and what it may not claim about work already filed. The
    // filing clause is appended to the proposal line rather than rendered on its own, so a
    // conversation that never filed gets byte-for-byte the prompt it gets today.
    // 2026-11-09-0620k: the proposal contract is gone — the skill says when to draft and that a draft is reviewed and
    // filed only on approval; what remains is the one fact only the server knows, whether this conversation filed work.
    private static string TurnContract(IReadOnlyList<SpecDialogTurn> transcript) =>
        FiledWorkContract(transcript).TrimStart('\n');

    // The trigger is the conversation's own filing record — a kept filing turn — not the
    // outcome type, which a failed turn, a twice-invalid proposal and a refused proposal all
    // share. So the clause reaches every turn kind, a proposal turn included.
    // 2026-10-13-9a59l: the clause used to say filed work cannot be CHANGED, full stop. In a
    // conversation bound to a ticket, approving a proposal continues that ticket's series by
    // spec id instead of filing a new one. What stays true is the part that matters — the
    // model never does it, an approval does, and reporting it as done is still false.
    private static string FiledWorkContract(IReadOnlyList<SpecDialogTurn> transcript) =>
        transcript.Any(turn => turn.Kind == SpecDialogTurnKind.Filing)
            ? "\nThis conversation has already filed work. You cannot change, close or re-cut "
              + "what was filed, and you must never report having done so: a reply that "
              + "describes filed work as merged, split, closed or otherwise altered is false. "
              + "Work you have not filed is what you PROPOSE, never what you have done. In a "
              + "conversation that belongs to existing work, approving a proposal CONTINUES that "
              + "work by spec id — specs that ran stay, repeated ids are re-cut, new ids "
              + "are appended — you propose it exactly as you would any other, the operator "
              + "approves, and reporting the continuation as done is as false as the rest."
            : string.Empty;

    // p0315e: one nudge covers every typed terminal outcome — a spec draft
    // (```yaml) and a bug/epic payload (```outcome) fail the same gate.
    public string BuildOutcomeFixNudge(string originalUserPrompt, string validationError) =>
        "The terminal outcome in your previous reply failed schema validation and was "
        + $"NOT shown to the operator. Validation error: {validationError}\n"
        + "Fix exactly what the error names and reply again with the full corrected "
        + "block (plus at most a line of framing). Do not repeat the invalid output.\n\n"
        + "Original task:\n" + originalUserPrompt;

}
