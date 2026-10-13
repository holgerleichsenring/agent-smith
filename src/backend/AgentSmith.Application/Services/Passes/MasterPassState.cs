using AgentSmith.Contracts.Progress;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Passes;

/// <summary>
/// 2026-10-13-9a59f: what a master pass leaves when it ends — the ledger, the run's changes, the verdict
/// the pass itself emitted (null when it emitted none) and the verdict the loop stands on (a pass without
/// one keeps the previous pass's).
/// </summary>
public sealed record MasterPassState(
    ProgressLedger Ledger,
    IReadOnlyList<CodeChange> Changes,
    MasterVerification? Emitted,
    MasterVerification? Standing);
