using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Services.BaseRefresh;

/// <summary>2026-11-24-6811c: one pass's answer — whole or not, the master's reason, and what its file tools wrote.</summary>
public sealed record BaseMergePass(bool Whole, string Reason, IReadOnlyList<CodeChange> Wrote);
