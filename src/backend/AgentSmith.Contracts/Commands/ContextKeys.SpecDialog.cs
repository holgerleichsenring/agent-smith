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
    /// <c>DialogImageSet</c> — 2026-09-20-3af8: the images the operator attached to this
    /// conversation, seeded by the turn runner. The master applies the vision flag and the
    /// per-turn ceiling to it; the set carries how many exist so the prompt can say so.
    /// </summary>
    public const string SpecDialogImages = "SpecDialogImages";

    /// <summary>
    /// Job id the master's ask_human questions publish under on the dialogue
    /// transport (spec-dialog: the session id, so answers from the same chat
    /// thread reach the waiting loop). Absent → ask_human reports itself
    /// unconfigured instead of blocking.
    /// </summary>
    public const string DialogueJobId = "DialogueJobId";

    /// <summary>
    /// <c>IFiledTicketWithdrawal</c> — 2026-09-22-9519: the door back out of a filing, seeded by
    /// the turn runner that has one. Seeded rather than injected into the master handler: the
    /// implementation reaches the session store and the tracker catalog, and a coding run on a
    /// server or a CLI composition that never opened a conversation must not have to construct it
    /// to run at all. Absent → the withdrawal tool is not on the turn's surface.
    /// </summary>
    public const string SpecDialogWithdrawal = "SpecDialogWithdrawal";

    /// <summary>
    /// 2026-09-25-8e51c: the ticket a bound conversation is grounded on — its title, the text as
    /// the conversation read it, whether the cap dropped part of it, and whether the ticket has
    /// moved since. Absent for a conversation that belongs to no ticket, which renders nothing.
    /// </summary>
    public const string SpecDialogTicket = "SpecDialogTicket";

    /// <summary>2026-09-27-481ba: reading that ticket from its tracker when the seeded copy was
    /// capped. Seeded only by a BOUND conversation's turn, so an unbound one carries no tool.</summary>
    public const string SpecDialogTicketReader = "SpecDialogTicketReader";

    /// <summary>2026-09-28-1da5c: what the TRACKER shows against that ticket — its pull requests,
    /// and its branches where a tracker links them. Seeded only by a bound conversation's turn.</summary>
    public const string SpecDialogTicketWork = "SpecDialogTicketWork";

    /// <summary>2026-09-28-1da5d: what THIS framework did about that ticket — its runs, their
    /// phases and the pull requests they opened. A different claim from what the board shows.</summary>
    public const string SpecDialogTicketRuns = "SpecDialogTicketRuns";

    /// <summary>2026-09-28-1da5e: the concrete strings this framework would use for this ticket —
    /// rendered from the code that decides them, so nothing can restate and then drift.</summary>
    public const string SpecDialogFrameworkFacts = "SpecDialogFrameworkFacts";

    /// <summary>2026-10-01-7f7ab: <c>IReadOnlyList&lt;DesignSource&gt;</c> — the turn's project's
    /// design sources, by name and secret NAME; a run reads them off ProjectConfig instead.</summary>
    public const string SpecDialogDesignSources = "SpecDialogDesignSources";

    /// <summary>2026-10-01-283de: <c>ResolvedProject</c> — the turn's project, from which render_reference
    /// builds its browser sandbox's spec; a run reads ProjectConfig instead.</summary>
    public const string SpecDialogProject = "SpecDialogProject";
}
