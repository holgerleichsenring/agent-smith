using AgentSmith.Infrastructure.Persistence.Entities;

namespace AgentSmith.Server.Services.Passes;

/// <summary>2026-10-13-9a59f: one model call as the passes read prices it.</summary>
public sealed record RunPassCall(long? EventSeq, int? StepIndex, string? SpecId, int? MasterPass, decimal CostUsd);

/// <summary>2026-10-13-9a59f: one step row, as far as attributing a call needs it.</summary>
public sealed record RunPassStep(long? EventSeq, int StepIndex, string? CommandName);

/// <summary>2026-10-13-9a59f: the rows one run's passes are read from.</summary>
public sealed record RunPassRecords(
    IReadOnlyList<RunPass> Passes,
    IReadOnlyList<RunLedgerTransition> Transitions,
    IReadOnlyList<RunPassCall> Calls,
    IReadOnlyList<RunPassStep> Steps);
