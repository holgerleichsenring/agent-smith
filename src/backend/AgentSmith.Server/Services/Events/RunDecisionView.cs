namespace AgentSmith.Server.Services.Events;

/// <summary>
/// p0388b: one logged decision from the RunDecision projection. <c>RecordedAt</c>
/// is the projection write time (the durable row carries no producer timestamp),
/// which is within the flush window of when the agent logged it.
/// </summary>
public sealed record RunDecisionView(
    int? StepIndex,
    string Name,
    string? Reason,
    DateTimeOffset RecordedAt);
