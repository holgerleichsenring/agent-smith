using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Passes;

namespace AgentSmith.Application.Services.Passes;

/// <summary>
/// 2026-10-13-9a59f: the end of one pass as the event carries it. 2026-11-22-96c8a: VerdictEmitted says the pass
/// ended on a report of its own, and the gate line whether a report stands; nothing is counted per done line.
/// </summary>
internal static class MasterPassEnd
{
    internal static MasterPassEndedEvent Event(
        MasterPassFrame pass, string endedBecause, string? reengageReason, MasterPassState state, DateTimeOffset at)
    {
        return new MasterPassEndedEvent(
            pass.RunId, at, pass.SpecId, pass.Ordinal, pass.PassInStep, pass.OpenedBecause, endedBecause,
            endedBecause == MasterPassReasons.Reengaged ? reengageReason : null,
            VerdictEmitted: state.ReportWritten,
            CriteriaMet: null,
            CriteriaTotal: null,
            Criteria: null,
            GateLine: state.HasReport ? "report written" : "no report",
            ActionablePending: state.Ledger.ActionablePending.Count,
            UnbackedDone: ProgressLedgerCoverage.UnbackedDoneSteps(state.Ledger, state.Changes).Count);
    }
}
