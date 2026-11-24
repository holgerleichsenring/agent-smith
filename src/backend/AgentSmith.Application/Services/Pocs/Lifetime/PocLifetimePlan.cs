using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Contracts.Runs;

namespace AgentSmith.Application.Services.Pocs.Lifetime;

/// <summary>
/// 2026-11-23-c81ee: what the lifetime pass does for one PoC — pure, in memory, writing nothing. With a deployed revision:
/// expired and seen -> delete; expired, unseen, no 'expired' entry at or after the expiry -> stop (also at 0 replicas, so
/// every unseen expiry gets one recorded stop); expired, unseen, with that entry -> nothing until unseen_delete_after_days
/// have passed since it, then delete. Without a revision: a blocked PoC whose latest upload was purged is removed; any
/// other (importing, a blocked PoC whose upload is kept, a pending delete) is left. The step's re-check reads the same
/// <see cref="CaseOf"/>; only the pass skips a PoC for its last run (<see cref="Skips"/>).
/// </summary>
public sealed class PocLifetimePlan
{
    /// <summary>A failed expiry run is retried at most once a day, so a broken PoC is not retried every pass.</summary>
    public static readonly TimeSpan RetryAfter = TimeSpan.FromHours(24); // not a limit: the spec's once-a-day retry cadence

    /// <summary>The PocLifetimeCases value due for <paramref name="poc"/> now, or null when nothing is.</summary>
    public string? CaseOf(PocLifetimeSubject poc, PocLifetimeState? state, PocLifetimeConfig config, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(poc);
        ArgumentNullException.ThrowIfNull(config);
        if (!PocStates.IsLive(poc.State)) return null;
        if (poc.Revision is null) return poc.State == PocStates.Blocked && poc.UploadGone ? PocLifetimeCases.Removal : null;
        if (state is not { IsExpired: true }) return null;
        if (state.IsSeen) return PocLifetimeCases.SeenDelete;
        if (poc.LatestExpiredAtTicks is not { } at || at < state.ExpiresAt.UtcTicks) return PocLifetimeCases.ExpiryStop;
        return now >= new DateTimeOffset(at, TimeSpan.Zero).AddDays(config.UnseenDeleteAfterDays) ? PocLifetimeCases.UnseenDelete : null;
    }

    /// <summary>Whether the pass leaves the PoC for now: its last run is live, or it is a failed poc-stop or poc-delete that
    /// finished less than <see cref="RetryAfter"/> ago — the Run row keeps no seed, so an owner's failed stop or delete
    /// backs off the same way.</summary>
    public bool Skips(PocLastRun? lastRun, bool lastRunLive, DateTimeOffset now)
    {
        if (lastRunLive) return true;
        if (lastRun is not { Status: RunStatuses.Failed, FinishedAt: { } finished }) return false;
        var retried = lastRun.Pipeline is PipelinePresets.PocStopName or PipelinePresets.PocDeleteName;
        return retried && now - finished < RetryAfter;
    }
}
