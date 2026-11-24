using System.Security.Claims;
using AgentSmith.Application.Services.Pocs.Lifetime;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Models;
using AgentSmith.Server.Security;
using AgentSmith.Server.Services.Pocs.Acts;

namespace AgentSmith.Server.Services.Pocs.Lifetime;

/// <summary>
/// 2026-11-23-c81ee: the lifetime fields of the PoC list and the detail page, all from <see cref="PocLifetime"/> — null for
/// a PoC that is not live or whose target pocs.targets no longer holds. The caller may extend when they may act on the
/// PoC and either it has fewer extensions than the owner's allowance or they hold pocs.admin.
/// </summary>
internal sealed class PocLifetimeViews(PocLifetime lifetime, PocActAccess access, TimeProvider clock)
{
    public PocLifetimeView? Of(ClaimsPrincipal user, Poc poc, PocsConfig pocs, long? latestExpiredAtTicks)
    {
        ArgumentNullException.ThrowIfNull(poc);
        ArgumentNullException.ThrowIfNull(pocs);
        if (!PocStates.IsLive(poc.State) || lifetime.TargetOf(pocs, poc.Target) is not { } target) return null;
        var state = lifetime.Of(PocActSubjects.Facts(poc), target, clock.GetUtcNow());
        var allowance = pocs.Lifetime.MaxOwnerExtensions;
        var mayExtend = access.MayAct(user, poc) && (poc.Extensions < allowance || access.IsAdmin(user));
        return new PocLifetimeView(state.ExpiresAt, state.DaysLeft, state.IsWindowOpen, state.IsExpired,
            lifetime.DeletesAt(state, latestExpiredAtTicks, pocs.Lifetime.UnseenDeleteAfterDays), poc.Extensions, allowance, mayExtend);
    }
}
