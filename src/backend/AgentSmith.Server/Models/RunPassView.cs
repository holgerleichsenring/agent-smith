using AgentSmith.Contracts.Passes;
using AgentSmith.Contracts.Runs;

namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-10-13-9a59f: one master pass. <see cref="Ordinal"/> is run-wide; <see cref="PassInSpec"/> is its
/// 1-based rank within its spec — the number a reader is shown. A pass whose calls are stamped and whose
/// end was never recorded reads ended "unknown" with nothing but its steps and cost.
/// </summary>
/// <param name="StepsMoved">The ledger transitions made inside the pass, in the order they were made.</param>
public sealed record RunPassView(
    int Ordinal,
    int PassInSpec,
    int? PassInStep,
    string? OpenedBecause,
    string EndedBecause,
    string? ReengageReason,
    bool? VerdictEmitted,
    int? CriteriaMet,
    int? CriteriaTotal,
    IReadOnlyList<MasterPassCriterion>? Criteria,
    string? GateLine,
    int? ActionablePending,
    int? UnbackedDone,
    DateTimeOffset? EndedAt,
    IReadOnlyList<LedgerTransitionView> StepsMoved,
    decimal CostUsd,
    int Calls);
