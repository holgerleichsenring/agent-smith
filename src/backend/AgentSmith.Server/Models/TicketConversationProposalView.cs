namespace AgentSmith.Server.Models;

/// <summary>2026-10-13-9a59h: the open proposal of a ticket's Work it out conversation and its drafts.</summary>
/// <param name="Kind">bug, spec or epic.</param>
public sealed record TicketConversationProposalView(string Kind, IReadOnlyList<TicketConversationDraftView> Drafts);

/// <summary>2026-10-13-9a59h: one drafted spec of an open proposal.</summary>
public sealed record TicketConversationDraftView(
    string SpecId, string Goal, IReadOnlyList<string> Repos, IReadOnlyList<string> Done);
