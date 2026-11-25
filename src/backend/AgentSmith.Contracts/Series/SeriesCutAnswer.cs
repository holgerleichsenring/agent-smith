namespace AgentSmith.Contracts.Series;

/// <summary>2026-11-25-9992f: what a change to a series' cut did — the answer, whether anything changed (false for a
/// refusal), and for an added ticket the filed ticket's id and key.</summary>
public sealed record SeriesCutAnswer(string Answer, bool Changed, string? TicketId = null, string? Key = null);
