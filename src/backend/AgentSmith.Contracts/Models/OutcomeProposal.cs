namespace AgentSmith.Contracts.Models;

/// <summary>
/// p0315e: the typed terminal outcome of a spec-dialog turn. PROPOSED to the
/// human and confirmed in-thread before anything routes, so an LLM
/// misclassification is caught, not executed. Discriminated union (mirrors
/// the SpecDraftOutcome style): answer / bug / one spec / an epic of linked
/// specs — the ceremony matches the work.
/// </summary>
public abstract record OutcomeProposal
{
    /// <summary>
    /// 2026-09-13-ed5a: the templates the turn that produced this outcome had open, stamped
    /// by the turn runner that opened them. It sits on the BASE record because the stamp is a
    /// fact about the analysis and not about the kind it resolved to — the epic parent is the
    /// only body that renders it today, and a type test in the runner would only decide, one
    /// kind at a time, which analyses are allowed to say what they read.
    /// </summary>
    public IReadOnlyList<TemplateProvenance> Templates { get; init; } = [];

    /// <summary>
    /// 2026-09-17-042ed: what the review of this proposal found, taken inside the turn that
    /// produced it. On the BASE record for the same reason the templates are: it is a fact about
    /// the analysis, and the four paths that show a proposal — the confirmation, the pane push,
    /// the stored row and a reload — then carry it without a signature each. Empty is a clean
    /// review and a review that could not be taken alike: neither is a fault.
    /// </summary>
    public IReadOnlyList<ProposalFinding> Findings { get; init; } = [];

    /// <summary>2026-10-13-8762i: every review round of the proposal's drafts, beside its findings.</summary>
    public IReadOnlyList<ProposalReviewRound> ReviewRounds { get; init; } = [];

    /// <summary>2026-10-13-8762i: the spec ids (or <see cref="ProposalReviewTokens.Series"/>) not reviewed yet,
    /// which cannot be approved; null when all were.</summary>
    public IReadOnlyList<string>? NotReviewed { get; init; }
}

/// <summary>A grounded answer — the turn ends with no artifact.</summary>
public sealed record AnswerOutcome : OutcomeProposal;

/// <summary>A small change worth a fix-bug ticket, not a spec.</summary>
public sealed record BugOutcome(BugTicketDraft Ticket) : OutcomeProposal;

/// <summary>One schema-valid spec draft.</summary>
public sealed record SpecOutcome(SpecDraft Draft) : OutcomeProposal;

/// <summary>
/// A feature too big for one spec: a parent spec plus ordered child specs
/// linked by requires: edges, each schema-valid on its own. 2026-10-13-8762j: <c>Contract</c> is its series contract, keyed by child ids.
/// </summary>
public sealed record EpicOutcome(SpecDraft Parent, IReadOnlyList<SpecDraft> Children, Specs.SeriesContract? Contract = null) : OutcomeProposal;
