namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-10-13-9a59e: one done criterion with its verdict (met | unmet | not_applicable; null while
/// unjudged), what it rests on, and an operator's overrule of it where one was recorded.
/// </summary>
public sealed record TicketSeriesCriterionView(
    string Text, string? Verdict, string? Note, string? Citation, TicketCriterionOverrule? Overrule);
