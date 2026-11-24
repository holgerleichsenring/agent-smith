namespace AgentSmith.Application.Services.Pocs.Lifetime;

/// <summary>
/// 2026-11-23-c81ee: a PoC's lifetime at one moment — when it expires, when its reminder window opened (the expiry minus
/// the largest reminder day), whether the window is open (open, not yet expired), whether it has expired, the whole days
/// left (0 once expired), the reminder due now (the smallest reminder day already reached, null before the window), and
/// whether its owner saw a reminder inside the window.
/// </summary>
public sealed record PocLifetimeState(
    DateTimeOffset ExpiresAt, DateTimeOffset WindowOpensAt, bool IsWindowOpen, bool IsExpired, int DaysLeft, int? DueReminderDays,
    bool IsSeen);
