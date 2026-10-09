using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// 2026-10-08-0781: enqueue as ONE merge statement on the provider — generation + 1, the stronger
/// origin, the pull request and channel kept when the new request names none, and the due time
/// pulled forward by a person's act but never by a run end or a sweep. No row: insert; a racing
/// insert's unique violation: merge again. Works on every provider through the translator.
/// </summary>
public sealed class ReworkNudgeWriter(IUnitOfWork uow, IUniqueViolationTranslator violations)
{
    public async Task UpsertAsync(ReworkNudgeRequest request, DateTimeOffset now, CancellationToken ct)
    {
        var due = (now + (request.Delay ?? TimeSpan.Zero)).UtcTicks;
        if (await MergeAsync(request, due, ct) > 0) return;
        var entry = uow.Add(New(request, due));
        try
        {
            await uow.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (violations.IsUniqueViolation(ex))
        {
            entry.State = EntityState.Detached;
            await MergeAsync(request, due, ct);
        }
    }

    private Task<int> MergeAsync(ReworkNudgeRequest r, long due, CancellationToken ct)
    {
        var origin = (int)r.Origin;
        var channel = r.Channel?.ToString();
        var late = r.IsLate;
        return uow.Set<ReworkNudge>()
            .Where(n => n.Project == r.Project && n.TicketId == r.TicketId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(n => n.Generation, n => n.Generation + 1)
                .SetProperty(n => n.Origin, n => n.Origin > origin ? n.Origin : origin)
                .SetProperty(n => n.PrUrl, n => r.PrUrl ?? n.PrUrl)
                .SetProperty(n => n.Channel, n => channel ?? n.Channel)
                .SetProperty(n => n.DueTicks, n => late
                    ? (n.DueTicks > due ? n.DueTicks : due)
                    : (n.DueTicks < due ? n.DueTicks : due)), ct);
    }

    private static ReworkNudge New(ReworkNudgeRequest r, long due) => new()
    {
        Project = r.Project, TicketId = r.TicketId, PrUrl = r.PrUrl, Channel = r.Channel?.ToString(),
        Origin = (int)r.Origin, DueTicks = due, Generation = 1,
    };
}
