namespace AgentSmith.Domain.Entities;

/// <summary>
/// A decision captured during plan generation or agentic execution.
/// </summary>
public sealed record PlanDecision(string Category, string Decision);
