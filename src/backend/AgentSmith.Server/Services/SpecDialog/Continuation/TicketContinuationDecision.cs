using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Server.Services.SpecDialog.Continuation;

/// <summary>
/// 2026-10-13-9a59l: one answer of <see cref="TicketContinuation.Decide"/> — the preview shows it and
/// the act applies it, so the two cannot disagree.
/// </summary>
/// <param name="Effect"><see cref="TicketContinuationEffects"/>.</param>
/// <param name="Reason">What a person is told the approval does, or why it does nothing.</param>
/// <param name="Executed">Specs of the series that already ran.</param>
/// <param name="Unstarted">Specs the series would hold that have not run.</param>
/// <param name="Drafts">What the effect records or files, re-id'd.</param>
public sealed record TicketContinuationDecision(
    string Effect,
    string Reason,
    int Executed,
    int Unstarted,
    string? BasisRunId,
    IReadOnlyList<TicketContinuationSpec> Specs,
    IReadOnlyList<SpecDraft> Drafts)
{
    /// <summary>The dispositions the approval record carries.</summary>
    public SeriesContinuation Continuation =>
        new([.. Specs.Select(s => new SpecDisposition(s.SpecId, s.Disposition))], BasisRunId);
}

/// <summary>One spec as the approval would leave it — <see cref="SpecDispositionKinds"/>.</summary>
public sealed record TicketContinuationSpec(string SpecId, string Goal, string Disposition, string? Note = null);
