namespace AgentSmith.Contracts.Commands;

/// <summary>
/// Spec-dialog PipelineContext keys (p0315b): the design-conversation inputs the
/// server seeds into a spec-dialog run and the reply slot it reads back out.
/// </summary>
public static partial class ContextKeys
{
    /// <summary>
    /// <c>IReadOnlyList&lt;SpecDialogTurn&gt;</c> — the ordered user/assistant
    /// transcript of the design thread, seeded by the server's turn runner.
    /// The design-partner master replies to the last user turn.
    /// </summary>
    public const string SpecDialogTranscript = "SpecDialogTranscript";

    /// <summary>
    /// <c>SpecDialogReplySlot</c> — mutable holder the server seeds so the
    /// CollectSpecDialogReply step can hand the master's final reply back to
    /// the turn runner (the pipeline context itself never leaves the use case).
    /// </summary>
    public const string SpecDialogReplySlot = "SpecDialogReplySlot";

    /// <summary>
    /// <c>OutcomeProposal</c> — the typed terminal outcome of the turn
    /// (p0315e), published by the AgenticMaster spec-dialog gate after
    /// validation; CollectSpecDialogReply copies it into the reply slot.
    /// </summary>
    public const string SpecDialogOutcome = "SpecDialogOutcome";

    /// <summary>
    /// <c>SpecDialogTurnKind</c> — set only when the reply the gate leaves is a notice in
    /// place of what the master wrote (2026-09-17-042ec); absent, the outcome names the kind.
    /// CollectSpecDialogReply copies it into the reply slot.
    /// </summary>
    public const string SpecDialogReplyKind = "SpecDialogReplyKind";

    /// <summary>
    /// <c>OutcomeProposal</c> — 2026-09-17-042ed: the proposal an EDIT turn is revising, seeded
    /// by the router that knows the turn is an edit. Absent on every other turn, so a turn after
    /// a filed or rejected proposal is shown no findings.
    /// </summary>
    public const string SpecDialogRevisedProposal = "SpecDialogRevisedProposal";

    /// <summary>
    /// Job id the master's ask_human questions publish under on the dialogue
    /// transport (spec-dialog: the session id, so answers from the same chat
    /// thread reach the waiting loop). Absent → ask_human reports itself
    /// unconfigured instead of blocking.
    /// </summary>
    public const string DialogueJobId = "DialogueJobId";
}
