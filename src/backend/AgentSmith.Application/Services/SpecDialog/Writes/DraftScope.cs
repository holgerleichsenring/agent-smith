namespace AgentSmith.Application.Services.SpecDialog.Writes;

/// <summary>2026-11-15-06fc: what a draft write is checked against — the turn's repositories, and whether the
/// conversation is bound to a ticket, which is where a comment belongs.</summary>
public sealed record DraftScope(IReadOnlyList<string> Repos, bool Bound);
