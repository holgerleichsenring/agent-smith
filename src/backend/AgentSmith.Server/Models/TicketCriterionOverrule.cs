namespace AgentSmith.Server.Models;

/// <summary>2026-10-13-9a59e: an operator's judgement of a criterion — what was true, why, who, when.</summary>
public sealed record TicketCriterionOverrule(string HumanStatus, string Reason, string Author, DateTimeOffset At);
