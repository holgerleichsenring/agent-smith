namespace AgentSmith.Infrastructure.Persistence.Models;

/// <summary>2026-10-01-283da: one image of a conversation as a transcript lists it — no bytes.</summary>
public sealed record ReferenceImageEntry(long Id, string MediaType, DateTimeOffset At);
