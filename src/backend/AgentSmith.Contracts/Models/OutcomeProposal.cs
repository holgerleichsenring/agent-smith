namespace AgentSmith.Contracts.Models;

/// <summary>
/// p0315e: the typed terminal outcome of a spec-dialog turn. PROPOSED to the
/// human and confirmed in-thread before anything routes, so an LLM
/// misclassification is caught, not executed. Discriminated union (mirrors
/// the SpecDraftOutcome style): answer / bug / one phase / an epic of linked
/// phases — the ceremony matches the work.
/// </summary>
public abstract record OutcomeProposal;

/// <summary>A grounded answer — the turn ends with no artifact.</summary>
public sealed record AnswerOutcome : OutcomeProposal;

/// <summary>A small change worth a fix-bug ticket, not a phase.</summary>
public sealed record BugOutcome(BugTicketDraft Ticket) : OutcomeProposal;

/// <summary>One schema-valid phase spec draft.</summary>
public sealed record PhaseOutcome(PhaseDraft Draft) : OutcomeProposal;

/// <summary>
/// A feature too big for one phase: a parent phase plus ordered child phases
/// linked by requires: edges, each schema-valid on its own.
/// </summary>
public sealed record EpicOutcome(PhaseDraft Parent, IReadOnlyList<PhaseDraft> Children) : OutcomeProposal;
