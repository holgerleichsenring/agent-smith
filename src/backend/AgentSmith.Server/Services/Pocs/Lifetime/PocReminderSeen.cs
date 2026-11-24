using System.Security.Claims;
using AgentSmith.Application.Services.Pocs.Lifetime;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.Pocs.Acts;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Server.Services.Tickets;

namespace AgentSmith.Server.Services.Pocs.Lifetime;

/// <summary>
/// 2026-11-23-c81ee: the owner saw a reminder — recorded only when the caller IS the PoC's owner and the reminder window
/// is open now (an admin's view never counts as the owner's, and a view after the expiry never counts). Anything else
/// records nothing; the route answers the same either way.
/// </summary>
internal sealed class PocReminderSeen(
    PocRepository pocs, IPocLifetimeStore store, PocLifetime lifetime, TicketProjects projects, TimeProvider clock,
    ILogger<PocReminderSeen> logger)
{
    public async Task<bool> RecordAsync(ClaimsPrincipal user, long pocId, CancellationToken ct)
    {
        if (await pocs.FindAsync(pocId, ct) is not { } poc || !PocStates.IsLive(poc.State)) return false;
        if (!string.Equals(poc.Owner, SpecDialogOwnership.PrincipalOf(user), StringComparison.Ordinal)) return false;
        if (lifetime.TargetOf(projects.Config().Pocs, poc.Target) is not { } target) return false;
        var now = clock.GetUtcNow();
        if (!lifetime.Of(PocActSubjects.Facts(poc), target, now).IsWindowOpen) return false;
        await store.RecordReminderSeenAsync(pocId, now.UtcTicks, ct);
        logger.LogInformation("The owner of PoC {PocId} saw its reminder", pocId);
        return true;
    }
}
