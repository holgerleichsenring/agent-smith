using AgentSmith.Contracts.Models;

namespace AgentSmith.Server.Models;

/// <summary>2026-10-13-9a59h: the open proposal of a ticket's Work it out conversation and its drafts.</summary>
/// <param name="Kind">bug, spec or epic.</param>
public sealed record TicketConversationProposalView(string Kind, IReadOnlyList<TicketConversationDraftView> Drafts)
{
    /// <summary>2026-10-13-8762i: what still stands against the proposal.</summary>
    public IReadOnlyList<ProposalFinding> Findings { get; init; } = [];

    /// <summary>2026-10-13-8762i: every review round of the proposal's drafts.</summary>
    public IReadOnlyList<ProposalReviewRound> ReviewRounds { get; init; } = [];

    /// <summary>2026-10-13-8762i: the spec ids (or "series") not reviewed yet; null when all were.</summary>
    public IReadOnlyList<string>? NotReviewed { get; init; }
}

/// <summary>2026-10-13-9a59h: one drafted spec of an open proposal.</summary>
/// <param name="RatifiedMock">2026-10-13-8762t: the mock version a person ratified for this spec; null with none.</param>
public sealed record TicketConversationDraftView(
    string SpecId, string Goal, IReadOnlyList<string> Repos, IReadOnlyList<string> Done, int? RatifiedMock = null);
