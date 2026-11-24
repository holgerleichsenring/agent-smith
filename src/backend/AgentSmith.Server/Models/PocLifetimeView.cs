namespace AgentSmith.Server.Models;

/// <summary>2026-11-23-c81ee: a PoC's lifetime as its card and its detail page show it — when it expires, the whole days
/// left, whether the reminder window is open (and it has not expired yet), whether it expired, when an expired PoC whose
/// owner saw no reminder is deleted unless extended (null until its expiry stop is recorded), its extensions, the owner's
/// allowance (pocs.lifetime.max_owner_extensions) and whether the caller may extend it now.</summary>
public sealed record PocLifetimeView(
    DateTimeOffset ExpiresAt, int DaysLeft, bool WindowOpen, bool Expired, DateTimeOffset? DeletesAt, int Extensions, int OwnerAllowance,
    bool MayExtend);
