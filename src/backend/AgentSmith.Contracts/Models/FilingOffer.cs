namespace AgentSmith.Contracts.Models;

/// <summary>
/// 2026-11-15-bf77: the design master's offer to file a draft — its one line of what filing would do, and the draft
/// object it was made on. A write replaces the store's draft, so the offer names a draft that is no longer current.
/// </summary>
public sealed record FilingOffer(string Line, ProposalDraft Draft);
