namespace AgentSmith.Contracts.Pocs;

/// <summary>2026-11-23-c81ee: what a PoC's lifetime is computed from — when it was created, the expiry only an extend run
/// stores (null: created + the target's lifetime_days), when its owner last saw a reminder (UTC ticks) and how many
/// extensions it had.</summary>
public sealed record PocLifetimeFacts(DateTimeOffset CreatedAt, long? ExpiresAtTicks, long? ReminderSeenAtTicks, int Extensions);
