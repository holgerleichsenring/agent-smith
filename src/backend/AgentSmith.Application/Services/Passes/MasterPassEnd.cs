using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Passes;

namespace AgentSmith.Application.Services.Passes;

/// <summary>
/// 2026-10-13-9a59f: the end of one pass as the event carries it. 2026-11-18-98fa: a pass's verdict is its own
/// finish_spec call; nothing is counted per done line, and the gate line names the standing finish_spec status.
/// </summary>
internal static class MasterPassEnd
{
    internal static MasterPassEndedEvent Event(
        MasterPassFrame pass, string endedBecause, string? reengageReason, MasterPassState state, DateTimeOffset at)
    {
        return new MasterPassEndedEvent(
            pass.RunId, at, pass.SpecId, pass.Ordinal, pass.PassInStep, pass.OpenedBecause, endedBecause,
            endedBecause == MasterPassReasons.Reengaged ? reengageReason : null,
            VerdictEmitted: state.Emitted is not null,
            CriteriaMet: null,
            CriteriaTotal: null,
            Criteria: null,
            GateLine: state.Standing is { } standing ? $"finish_spec: {standing.Status}" : "finish_spec not called",
            ActionablePending: state.Ledger.ActionablePending.Count,
            UnbackedDone: ProgressLedgerCoverage.UnbackedDoneSteps(state.Ledger, state.Changes).Count);
    }
}
