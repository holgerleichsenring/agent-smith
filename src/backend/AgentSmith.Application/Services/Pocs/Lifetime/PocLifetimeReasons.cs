using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Pocs;

namespace AgentSmith.Application.Services.Pocs.Lifetime;

/// <summary>2026-11-23-c81ee: the audit act and the reason a lifetime case records — 'expired' for the expiry cases,
/// 'deleted' for a removal — each naming the value and the key that decided it.</summary>
public sealed class PocLifetimeReasons
{
    public PocAuditAct Of(string lifetimeCase, PocsConfig pocs)
    {
        ArgumentNullException.ThrowIfNull(pocs);
        return lifetimeCase switch
        {
            PocLifetimeCases.ExpiryStop => new(PocActs.Expired, "stopped, not deleted: no reminder was seen before expiry"),
            PocLifetimeCases.SeenDelete => new(PocActs.Expired, "deleted after the owner saw a reminder"),
            PocLifetimeCases.UnseenDelete => new(PocActs.Expired, $"deleted {pocs.Lifetime.UnseenDeleteAfterDays} days after its expiry "
                + $"stop: no reminder was seen and it was not extended ({PocLifetimeConfig.UnseenDeleteAfterDaysKey})"),
            PocLifetimeCases.Removal => new(PocActs.Deleted, "removed with its upload: blocked and never deployed, the upload was "
                + $"purged after {pocs.BlockedRetentionDays} days ({PocsConfig.BlockedRetentionDaysKey})"),
            _ => throw new ArgumentOutOfRangeException(nameof(lifetimeCase), lifetimeCase, "not a PoC lifetime case"),
        };
    }
}
