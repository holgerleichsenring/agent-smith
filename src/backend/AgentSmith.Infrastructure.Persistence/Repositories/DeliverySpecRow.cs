namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>2026-10-25-0099b: one spec an attempt executed — its position, goal and standing.</summary>
public sealed record DeliverySpecRow(string RunId, int Ordinal, string Title, string Status);
