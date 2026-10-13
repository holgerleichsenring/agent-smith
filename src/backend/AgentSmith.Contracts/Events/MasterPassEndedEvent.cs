using AgentSmith.Contracts.Passes;

namespace AgentSmith.Contracts.Events;

/// <summary>
/// 2026-10-13-9a59f: one master pass of a spec ended. <see cref="Ordinal"/> is run-wide and survives a
/// park and a repair; <see cref="PassInStep"/> is the pass within one master step (0 = its first loop).
/// <see cref="ReengageReason"/> is set only when the pass ended re-engaged. The criteria counts and the
/// per-criterion verdicts are this pass's own — null when it emitted no verdict, the counts also null on
/// a verdict-level refusal. <see cref="GateLine"/> is the acceptance gate's own description.
/// </summary>
public sealed record MasterPassEndedEvent(
    string RunId,
    DateTimeOffset Timestamp,
    string SpecId,
    int Ordinal,
    int PassInStep,
    string OpenedBecause,
    string EndedBecause,
    string? ReengageReason,
    bool VerdictEmitted,
    int? CriteriaMet,
    int? CriteriaTotal,
    IReadOnlyList<MasterPassCriterion>? Criteria,
    string GateLine,
    int ActionablePending,
    int UnbackedDone)
    : RunEvent(RunId, EventType.MasterPassEnded, Timestamp);
