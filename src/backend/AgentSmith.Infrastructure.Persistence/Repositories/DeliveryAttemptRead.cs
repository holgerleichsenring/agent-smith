namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>2026-10-25-0099b: the window's attempts, newest first, and whether the ceiling cut the oldest off.</summary>
public sealed record DeliveryAttemptRead(IReadOnlyList<DeliveryAttemptRow> Rows, bool Truncated);
