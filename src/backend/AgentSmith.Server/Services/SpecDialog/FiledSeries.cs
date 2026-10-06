using AgentSmith.Contracts.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-10-06-03c7c: what a filing stores — the series' code-minted base id and its drafts,
/// already re-id'd to it, in the order the one run will work them.
/// </summary>
public sealed record FiledSeries(string Id, IReadOnlyList<PhaseDraft> Drafts);
