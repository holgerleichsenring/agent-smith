using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Server.Services.SpecDialog.Continuation;

/// <summary>
/// 2026-10-13-8762j: the series contract a continuation records. The proposal's own contract, re-keyed with the
/// map that gave its drafts their continued ids — so the two cannot disagree — and narrowed to the specs the
/// series still holds (every disposition but Dropped); the basis record's contract only when the proposal
/// carries none.
/// </summary>
public static class ContinuedContract
{
    public static SeriesContractWithin Of(
        OutcomeProposal proposal, TicketContinuationDecision decision, TicketContinuationState current)
    {
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(current);
        if ((proposal as EpicOutcome)?.Contract is not { } contract) return new(current.Record?.Set.Contract, []);
        var held = decision.Specs.Where(s => s.Disposition != SpecDispositionKinds.Dropped).Select(s => s.SpecId);
        return contract.Rekeyed(decision.Ids).Within(decision.Drafts.Select(d => d.SpecId).Concat(held));
    }
}
