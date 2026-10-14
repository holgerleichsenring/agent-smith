using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Infrastructure.Persistence.Services;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// 2026-10-08-0781: the claim protocol over the nudge rows. Claim: compare-and-set on the generation
/// a due row had, taking a token and pushing due five minutes out — a crashed worker's claim is free
/// again once that passes. Finish: delete at the claimed generation; a merge since leaves the row,
/// freed and due now. Reschedule: compare-and-set at the generation, a new due time.
/// <para>2026-10-13-9a59b: the claim resets the row's origin to Refresh — the claimed origin rides on
/// the claim — so a Finish at a stale generation leaves only the origins merged since, and a Refresh
/// merged during handling never re-runs the handler. A Reschedule, at either generation, restores the
/// higher of the current and the claimed origin. The token carries the claimed origin, so a claim
/// that expires with its crashed worker is served again at that origin.</para>
/// </summary>
public sealed class ReworkNudgeRepository(IUnitOfWork unitOfWork, IUniqueViolationTranslator violations, TimeProvider time)
{
    public static readonly TimeSpan ClaimLease = TimeSpan.FromMinutes(5);

    public Task EnqueueAsync(ReworkNudgeRequest request, CancellationToken ct) =>
        new ReworkNudgeWriter(unitOfWork, violations).UpsertAsync(request, time.GetUtcNow(), ct);

    public async Task<IReadOnlyList<ClaimedReworkNudge>> ClaimDueAsync(int max, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var due = await unitOfWork.Set<ReworkNudge>().AsNoTracking()
            .Where(n => n.DueTicks <= now.UtcTicks).OrderBy(n => n.DueTicks).Take(max).ToListAsync(ct);
        var claimed = new List<ClaimedReworkNudge>();
        foreach (var n in due)
        {
            var origin = Math.Max(n.Origin, ExpiredClaimOrigin(n.ClaimToken));
            var token = $"{origin}:{Guid.NewGuid():N}";
            var won = await unitOfWork.Set<ReworkNudge>()
                .Where(x => x.Project == n.Project && x.TicketId == n.TicketId
                    && x.Generation == n.Generation && x.DueTicks <= now.UtcTicks)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.ClaimToken, token)
                    .SetProperty(x => x.Origin, (int)ReworkNudgeOrigin.Refresh)
                    .SetProperty(x => x.DueTicks, (now + ClaimLease).UtcTicks), ct);
            if (won > 0)
                claimed.Add(new(n.Project, n.TicketId, (ReworkNudgeOrigin)origin, n.PrUrl, n.Channel, n.Generation, token, n.Tries));
        }
        return claimed;
    }

    public async Task FinishAsync(ClaimedReworkNudge nudge, CancellationToken ct)
    {
        var deleted = await Mine(nudge).Where(n => n.Generation == nudge.Generation).ExecuteDeleteAsync(ct);
        if (deleted > 0) return;
        await Mine(nudge).ExecuteUpdateAsync(s => s.SetProperty(n => n.ClaimToken, (string?)null)
            .SetProperty(n => n.DueTicks, time.GetUtcNow().UtcTicks), ct);
    }

    public async Task RescheduleAsync(ClaimedReworkNudge nudge, TimeSpan delay, CancellationToken ct)
    {
        var claimed = (int)nudge.Origin;
        var moved = await Mine(nudge).Where(n => n.Generation == nudge.Generation)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ClaimToken, (string?)null)
                .SetProperty(n => n.Tries, n => n.Tries + 1)
                .SetProperty(n => n.Origin, n => n.Origin > claimed ? n.Origin : claimed)
                .SetProperty(n => n.DueTicks, (time.GetUtcNow() + delay).UtcTicks), ct);
        if (moved > 0) return;
        await Mine(nudge).ExecuteUpdateAsync(s => s.SetProperty(n => n.ClaimToken, (string?)null)
            .SetProperty(n => n.Origin, n => n.Origin > claimed ? n.Origin : claimed)
            .SetProperty(n => n.DueTicks, time.GetUtcNow().UtcTicks), ct);
    }

    // A claim whose worker died left its row at Refresh; its token still names the origin it claimed.
    private static int ExpiredClaimOrigin(string? token) =>
        token?.Split(':', 2) is [var origin, _] && int.TryParse(origin, out var value) ? value : (int)ReworkNudgeOrigin.Refresh;

    private IQueryable<ReworkNudge> Mine(ClaimedReworkNudge nudge) =>
        unitOfWork.Set<ReworkNudge>().Where(n => n.Project == nudge.Project && n.TicketId == nudge.TicketId
            && n.ClaimToken == nudge.ClaimToken);
}
