using AgentSmith.Contracts.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-10-06-03c7c: what a filing stores — the series' code-minted base id and its drafts,
/// already re-id'd to it, in the order the one run will work them.
/// </summary>
public sealed record FiledSeries(string Id, IReadOnlyList<SpecDraft> Drafts)
{
    /// <summary>2026-10-13-8762j: the series contract keyed by the filed ids; null when none.</summary>
    public AgentSmith.Contracts.Specs.SeriesContract? Contract { get; init; }

    /// <summary>2026-10-13-8762l: the approved proposal's review rounds, re-keyed onto the filed ids.</summary>
    public IReadOnlyList<ProposalReviewRound> ReviewRounds { get; init; } = [];

    /// <summary>The filed id of each draft mapped to the spec id the proposal gave it; null read as empty.</summary>
    public IReadOnlyDictionary<string, string>? Sources { get; init; }
}
