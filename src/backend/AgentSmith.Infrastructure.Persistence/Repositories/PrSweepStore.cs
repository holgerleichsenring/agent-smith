using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// 2026-10-08-10b0: the PR sweep's rows. Every move a launch depends on is a compare-and-set on the
/// value the sweep read — equality on the old head, label flag or comment mark — so two cycles that
/// read the same pull request launch for it once; the one whose write lost starts nothing.
/// </summary>
public sealed class PrSweepStore(IUnitOfWork uow, IUniqueViolationTranslator violations)
{
    public Task<PrSweepRepository?> RepositoryAsync(string repository, CancellationToken ct) =>
        uow.Set<PrSweepRepository>().AsNoTracking().FirstOrDefaultAsync(r => r.Repository == repository, ct);

    public async Task MarkSweptAsync(string repository, bool initialised, DateTimeOffset at, CancellationToken ct)
    {
        var ticks = at.UtcTicks;
        var updated = await uow.Set<PrSweepRepository>().Where(r => r.Repository == repository)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.LastSweptTicks, ticks).SetProperty(r => r.Initialised, initialised), ct);
        if (updated == 0) await InsertAsync(new PrSweepRepository { Repository = repository, Initialised = initialised, LastSweptTicks = ticks }, ct);
    }

    public async Task<IReadOnlyDictionary<string, PrSweepState>> StatesAsync(string repository, CancellationToken ct) =>
        await uow.Set<PrSweepState>().AsNoTracking().Where(s => s.Repository == repository).ToDictionaryAsync(s => s.Number, ct);

    /// <summary>Initialisation: the pull request as it is now, overwriting what was there.</summary>
    public async Task RecordAsync(PrSweepState state, CancellationToken ct)
    {
        var updated = await uow.Set<PrSweepState>().Where(s => s.Repository == state.Repository && s.Number == state.Number)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ReviewedHead, state.ReviewedHead).SetProperty(x => x.LabelPresent, state.LabelPresent)
                .SetProperty(x => x.CommentsSeenTicks, state.CommentsSeenTicks).SetProperty(x => x.CommentsSeenId, state.CommentsSeenId), ct);
        if (updated == 0) await InsertAsync(state, ct);
    }

    public Task<bool> TryInsertAsync(PrSweepState state, CancellationToken ct) => InsertAsync(state, ct);

    public async Task<bool> TryMoveHeadAsync(string repository, string number, string? from, string? to, CancellationToken ct) =>
        await One(repository, number).Where(s => s.ReviewedHead == from).ExecuteUpdateAsync(s => s.SetProperty(x => x.ReviewedHead, to), ct) > 0;

    public async Task<bool> TrySetLabelAsync(string repository, string number, bool from, bool to, CancellationToken ct) =>
        await One(repository, number).Where(s => s.LabelPresent == from).ExecuteUpdateAsync(s => s.SetProperty(x => x.LabelPresent, to), ct) > 0;

    public async Task<bool> TryAdvanceCommentsAsync(
        string repository, string number, long fromTicks, string fromId, long toTicks, string toId, CancellationToken ct) =>
        await One(repository, number).Where(s => s.CommentsSeenTicks == fromTicks && s.CommentsSeenId == fromId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.CommentsSeenTicks, toTicks).SetProperty(x => x.CommentsSeenId, toId), ct) > 0;

    /// <summary>
    /// 2026-10-09-af10: a sweep-launched review's outcome. A success clears the run of failures; a
    /// failure adds one by compare-and-set and returns the new count, so exactly one caller sees the
    /// threshold reached. A pull request without a row (closed meanwhile) counts nothing.
    /// </summary>
    public async Task<int> RecordReviewOutcomeAsync(string repository, string number, bool succeeded, CancellationToken ct)
    {
        if (succeeded)
        {
            await ResetFailedReviewsAsync(repository, number, ct);
            return 0;
        }
        while (true)
        {
            var current = await One(repository, number).AsNoTracking().Select(s => (int?)s.FailedReviews).FirstOrDefaultAsync(ct);
            if (current is not { } from) return 0;
            if (await One(repository, number).Where(s => s.FailedReviews == from)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.FailedReviews, from + 1), ct) > 0)
                return from + 1;
        }
    }

    /// <summary>2026-10-09-af10: a person asked again — the sweep launches for the pull request once more.</summary>
    public Task ResetFailedReviewsAsync(string repository, string number, CancellationToken ct) =>
        One(repository, number).ExecuteUpdateAsync(s => s.SetProperty(x => x.FailedReviews, 0), ct);

    /// <summary>A complete list of open pull requests says the others closed or merged.</summary>
    public Task PruneAsync(string repository, IReadOnlyCollection<string> open, CancellationToken ct) =>
        uow.Set<PrSweepState>().Where(s => s.Repository == repository && !open.Contains(s.Number)).ExecuteDeleteAsync(ct);

    private IQueryable<PrSweepState> One(string repository, string number) =>
        uow.Set<PrSweepState>().Where(s => s.Repository == repository && s.Number == number);

    private async Task<bool> InsertAsync(object row, CancellationToken ct)
    {
        var entry = uow.Add(row);
        try
        {
            await uow.SaveChangesAsync(ct);
            entry.State = EntityState.Detached;
            return true;
        }
        catch (DbUpdateException ex) when (violations.IsUniqueViolation(ex))
        {
            entry.State = EntityState.Detached;
            return false;
        }
    }
}
