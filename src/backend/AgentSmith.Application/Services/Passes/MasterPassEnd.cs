using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Passes;
using AgentSmith.Contracts.Runs;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Passes;

/// <summary>
/// 2026-10-13-9a59f: the end of one pass as the event carries it. The counts and the per-criterion
/// verdicts are the pass's OWN — a pass that emitted no verdict reports none rather than the previous
/// pass's; the gate line is the judgement the loop decided on, in the gate's own words.
/// </summary>
internal static class MasterPassEnd
{
    internal static MasterPassEndedEvent Event(
        MasterPassFrame pass, string endedBecause, string? reengageReason, MasterPassState state,
        IReadOnlyList<string> criteria, DateTimeOffset at)
    {
        var produced = state.Changes.Count > 0;
        var own = state.Emitted is null ? null : MasterAcceptanceGate.Judge(state.Emitted, criteria, produced);
        var counted = own is { Refusal: null, Pairings.Count: > 0 };
        return new MasterPassEndedEvent(
            pass.RunId, at, pass.SpecId, pass.Ordinal, pass.PassInStep, pass.OpenedBecause, endedBecause,
            endedBecause == MasterPassReasons.Reengaged ? reengageReason : null,
            VerdictEmitted: state.Emitted is not null,
            CriteriaMet: counted ? own!.Pairings.Count(p => p.Satisfied) : null,
            CriteriaTotal: counted ? own!.Pairings.Count : null,
            Criteria: state.Emitted?.AcceptanceDispositions?
                .Select(d => new MasterPassCriterion(d.Criterion, Status(d.Status), d.Evidence)).ToList(),
            GateLine: MasterAcceptanceGate.Judge(state.Standing, criteria, produced).Describe(),
            ActionablePending: state.Ledger.ActionablePending.Count,
            UnbackedDone: ProgressLedgerCoverage.UnbackedDoneSteps(state.Ledger, state.Changes).Count);
    }

    private static string Status(AcceptanceStatus status) => status switch
    {
        AcceptanceStatus.Met => AcceptanceCriterionStatuses.Met,
        AcceptanceStatus.NotApplicable => AcceptanceCriterionStatuses.NotApplicable,
        _ => AcceptanceCriterionStatuses.Unmet,
    };
}
