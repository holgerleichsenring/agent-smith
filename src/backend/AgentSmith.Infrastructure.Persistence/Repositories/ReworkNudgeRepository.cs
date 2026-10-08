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
            var token = Guid.NewGuid().ToString("N");
            var won = await unitOfWork.Set<ReworkNudge>()
                .Where(x => x.Project == n.Project && x.TicketId == n.TicketId
                    && x.Generation == n.Generation && x.DueTicks <= now.UtcTicks)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.ClaimToken, token)
                    .SetProperty(x => x.DueTicks, (now + ClaimLease).UtcTicks), ct);
            if (won > 0)
                claimed.Add(new(n.Project, n.TicketId, (ReworkNudgeOrigin)n.Origin, n.PrUrl, n.Channel, n.Generation, token, n.Tries));
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
        var moved = await Mine(nudge).Where(n => n.Generation == nudge.Generation)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ClaimToken, (string?)null)
                .SetProperty(n => n.Tries, n => n.Tries + 1)
                .SetProperty(n => n.DueTicks, (time.GetUtcNow() + delay).UtcTicks), ct);
        if (moved == 0) await FinishAsync(nudge, ct);
    }

    private IQueryable<ReworkNudge> Mine(ClaimedReworkNudge nudge) =>
        unitOfWork.Set<ReworkNudge>().Where(n => n.Project == nudge.Project && n.TicketId == nudge.TicketId
            && n.ClaimToken == nudge.ClaimToken);
}
