using System.Text;
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
/// 2026-09-17-042ec: the contract says whether this turn may propose. The gate refuses a
/// proposal from a turn that may not, so saying it first spares the turn a second model call.
/// </para>
/// <para>
/// 2026-09-22-deda: the contract also says what a conversation that has already filed work
/// cannot do to it. A capability, not a state: it names no ticket and claims nothing about a
/// tracker, so it cannot go stale between the filing and the turn that reads it.
/// </para>
/// </summary>
public sealed class SpecDialogPromptFactory : ISpecDialogPromptFactory
{
    // A request for work is answered with what a proposal would need settled; a question is
    // answered as it is asked, in plain prose, not padded into that shape.
    private const string FirstAnswer =
        "When the operator asks for work, answer with what you found in the code (with the files "
        + "you read), the edge cases, and the open questions only the operator can decide. "
        + "A question is answered as it is asked.";

    public string Build(PipelineContext pipeline, int imagesExisting, int imagesCarried)
    {
        var transcript = pipeline.TryGet<IReadOnlyList<SpecDialogTurn>>(
            ContextKeys.SpecDialogTranscript, out var t) && t is not null ? t : [];
        if (transcript.Count == 0)
            throw new InvalidOperationException(
                "Spec-dialog run has an empty transcript — the turn runner must seed "
                + $"ContextKeys.{nameof(ContextKeys.SpecDialogTranscript)} with at least the opening user turn.");

        return $"""
            ## Design conversation
            The transcript of this design thread so far, oldest first. Respond to the
            LAST user turn; earlier turns are context you already produced or received.

            {RenderTranscript(transcript)}
            {SpecDialog.SeededTicketSection.Render(pipeline)}
            {SpecDialogRevisionSection.Render(pipeline)}

            ## Your reply
            Write the reply text now — it is delivered verbatim to the chat thread.
            Answer from the code map above when it suffices; read source files through
            your tools only when the question needs real file content.
            {DialogImageNote.Render(imagesExisting, imagesCarried)}
            {TurnContract(transcript)}
            """;
    }

    // 2026-09-22-deda: one contract, composed per turn from the transcript the turn began
    // with — what it may propose, and what it may not claim about work already filed. The
    // filing clause is appended to the proposal line rather than rendered on its own, so a
    // conversation that never filed gets byte-for-byte the prompt it gets today.
    private static string TurnContract(IReadOnlyList<SpecDialogTurn> transcript) =>
        ProposalContract(SpecDialogProposalRule.MayPropose(transcript)) + FiledWorkContract(transcript);

    // The trigger is the conversation's own filing record — a kept filing turn — not the
    // outcome type, which a failed turn, a twice-invalid proposal and a refused proposal all
    // share. So the clause reaches every turn kind, a proposal turn included.
    // 2026-09-25-8e51e: the clause used to say filed work cannot be CHANGED, full stop. That is
    // no longer true: approving an amendment rewrites the framework's region of the bound
    // ticket and re-records the approved set. What stays true is the part that matters — the
    // model never does it, an approval does, and reporting it as done is still false.
    private static string FiledWorkContract(IReadOnlyList<SpecDialogTurn> transcript) =>
        transcript.Any(turn => turn.Kind == SpecDialogTurnKind.Filing)
            ? "\nThis conversation has already filed work. You cannot change, close or re-cut "
              + "what was filed, and you must never report having done so: a reply that "
              + "describes filed work as merged, split, closed or otherwise altered is false. "
              + "Work you have not filed is what you PROPOSE, never what you have done. A "
              + "proposal may also be approved AS AN AMENDMENT of the work this conversation "
              + "belongs to, which rewrites it from the approved specification — you propose it "
              + "exactly as you would any other, the operator chooses the amendment, and "
              + "reporting one as done is as false as the rest."
            : string.Empty;

    private static string ProposalContract(bool mayPropose) => mayPropose
        ? "The operator has replied to a discussion, so this turn MAY propose: draft a fenced "
          + "```yaml phase spec (or an ```outcome block) only when the conversation has converged "
          + "on work that warrants it — otherwise reply with no artifact."
        : "This turn MAY NOT propose: the operator has not yet replied to a discussion of this "
          + "work. Draft no ```yaml or ```outcome block — it would be refused. " + FirstAnswer;

    // p0315e: one nudge covers every typed terminal outcome — a phase draft
    // (```yaml) and a bug/epic payload (```outcome) fail the same gate.
    public string BuildOutcomeFixNudge(string originalUserPrompt, string validationError) =>
        "The terminal outcome in your previous reply failed schema validation and was "
        + $"NOT shown to the operator. Validation error: {validationError}\n"
        + "Fix exactly what the error names and reply again with the full corrected "
        + "block (plus at most a line of framing). Do not repeat the invalid output.\n\n"
        + "Original task:\n" + originalUserPrompt;

    public string BuildProposalRefusalNudge(string originalUserPrompt) =>
        "Your previous reply proposed work before the operator had replied to a discussion "
        + "of it, so it was NOT shown. Reply again with no ```yaml or ```outcome block. "
        + FirstAnswer + "\n\n"
        + "Original task:\n" + originalUserPrompt;

    private static string RenderTranscript(IReadOnlyList<SpecDialogTurn> transcript)
    {
        var sb = new StringBuilder();
        foreach (var turn in transcript)
            sb.AppendLine($"[{turn.Role}] {turn.Text}");
        return sb.ToString().TrimEnd();
    }
}
