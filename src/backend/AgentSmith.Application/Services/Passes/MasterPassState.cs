using AgentSmith.Contracts.Progress;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Services.Passes;

/// <summary>
/// 2026-10-13-9a59f: what a master pass leaves when it ends — the ledger and the run's changes. 2026-11-22-96c8a:
/// and whether the pass ended on a report of its own, and whether a report stands after it (a cut pass keeps the
/// previous pass's).
/// </summary>
public sealed record MasterPassState(
    ProgressLedger Ledger,
    IReadOnlyList<CodeChange> Changes,
    bool ReportWritten,
    bool HasReport);
