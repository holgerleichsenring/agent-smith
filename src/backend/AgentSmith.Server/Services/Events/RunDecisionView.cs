namespace AgentSmith.Server.Services.Events;

/// <summary>
/// p0388b: one logged decision from the RunDecision projection. <c>RecordedAt</c>
/// is the projection write time (the durable row carries no producer timestamp),
/// which is within the flush window of when the agent logged it.
/// p0388c: <c>Category</c> is the producer's own classification; null on rows
/// written before it was projected, which render without the segment.
/// </summary>
public sealed record RunDecisionView(
    int? StepIndex,
    string Name,
    string? Reason,
    string? Category,
    DateTimeOffset RecordedAt);
