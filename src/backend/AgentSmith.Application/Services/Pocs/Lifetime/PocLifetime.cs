using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Pocs;

namespace AgentSmith.Application.Services.Pocs.Lifetime;

/// <summary>
/// 2026-11-23-c81ee: a PoC's expiry and reminders, computed — pure and in memory. It expires at the expiry an extend run
/// stored, else at its creation plus its target's lifetime_days. The reminder window opens at the expiry minus the largest
/// reminder day; a reminder for day d is due while expiry - d &lt;= now &lt; expiry. A reminder is SEEN when the owner's
/// seen time lies in [window open, expiry): a view after the expiry never counts, and one from before an extension falls
/// outside the new window. Nothing else about reminders is stored.
/// </summary>
public sealed class PocLifetime
{
    public PocLifetimeState Of(PocLifetimeFacts facts, PocTargetConfig target, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(target);
        var expires = facts.ExpiresAtTicks is { } ticks ? new DateTimeOffset(ticks, TimeSpan.Zero) : facts.CreatedAt.AddDays(target.LifetimeDays);
        var largest = target.ReminderDays.Count == 0 ? 0 : target.ReminderDays.Max();
        var opens = expires.AddDays(-largest);
        var expired = now >= expires;
        var due = target.ReminderDays.Where(d => expires.AddDays(-d) <= now).Order().Cast<int?>().FirstOrDefault();
        var seen = facts.ReminderSeenAtTicks is { } at && at >= opens.UtcTicks && at < expires.UtcTicks;
        return new PocLifetimeState(expires, opens, !expired && now >= opens, expired, expired ? 0 : (int)Math.Ceiling((expires - now).TotalDays),
            expired ? null : due, seen);
    }

    /// <summary>When the PoC expired, while it is expired; null while it is not, or when its facts or its target are not
    /// known.</summary>
    public DateTimeOffset? ExpiredOn(PocLifetimeFacts? facts, PocTargetConfig? target, DateTimeOffset now) =>
        facts is null || target is null ? null : Of(facts, target, now) is { IsExpired: true } state ? state.ExpiresAt : null;

    /// <summary>The target a PoC names, or null when pocs.targets no longer holds it.</summary>
    public PocTargetConfig? TargetOf(PocsConfig pocs, string target)
    {
        ArgumentNullException.ThrowIfNull(pocs);
        return pocs.Targets.FirstOrDefault(t => string.Equals(t.Name, target, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>When an expired PoC whose owner saw no reminder is deleted: <paramref name="unseenDeleteAfterDays"/> after its
    /// latest 'expired' audit entry, when that entry lies at or after the expiry (an expiry stop of this expiry); else
    /// null.</summary>
    public DateTimeOffset? DeletesAt(PocLifetimeState state, long? latestExpiredAtTicks, int unseenDeleteAfterDays)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!state.IsExpired || state.IsSeen || latestExpiredAtTicks is not { } at || at < state.ExpiresAt.UtcTicks) return null;
        return new DateTimeOffset(at, TimeSpan.Zero).AddDays(unseenDeleteAfterDays);
    }
}
