namespace AgentSmith.Infrastructure.Persistence.Models;

/// <summary>2026-10-01-283da: one image of a conversation as a transcript lists it — no bytes.
/// 2026-10-08-e8b9g: with its set id and size; both null for a legacy row not copied yet, which
/// is neither citable nor counted until the leader copies it.</summary>
public sealed record ReferenceImageEntry(long Id, string MediaType, DateTimeOffset At, string? SetId = null, long? Bytes = null);
