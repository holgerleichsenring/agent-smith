using AgentSmith.Contracts.Tickets;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;

namespace AgentSmith.Tests.Persistence;

/// <summary>
/// 2026-10-15-114fa: a ticket's close mark is one row per tracker connection and ticket key, over the shipped
/// migrations — the newest close overwrites it, racing writers keep one row, a Reopen between the insert and the
/// re-read retries the insert once, and a clear answers whether a row went.
/// </summary>
public sealed class ClosedTicketStoreTests : IDisposable
{
    private const string Tracker = "sample-tracker";
    private static readonly DateTimeOffset T = new(2026, 10, 15, 9, 0, 0, TimeSpan.Zero);
    private readonly ServerStateStore _store = new();

    private ClosedTicketRepository Store => new(_store.ScopeFactory);

    private static TicketClosedMark Mark(string ticket, string by = "Ada", string tracker = Tracker, string? note = null) =>
        new(tracker, $"jira-{ticket}", ticket.ToUpperInvariant(), "sample", T, by, note, [$"https://github.com/o/api/pull/{ticket}"], true);

    [Fact]
    public async Task ClosedTicketStore_SetTwice_KeepsOneRowWithTheSecondDecision()
    {
        await Store.SetAsync(Mark("a-1"), default);
        await Store.SetAsync(Mark("a-1", by: "Grace", note: "not needed") with { ClosedPullRequests = [], TrackerClosed = false }, default);

        (await Store.OfAsync(Tracker, ["jira-a-1"], default)).Should().ContainSingle().Which.Should().BeEquivalentTo(
            Mark("a-1", by: "Grace", note: "not needed") with { ClosedPullRequests = [], TrackerClosed = false });
        await using var db = _store.Context();
        (await db.Set<ClosedTicket>().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task ClosedTicketStore_TwoWritersRacing_KeepOneRow()
    {
        var fired = false;
        ServerStateStore? store = null;
        using var racing = new ServerStateStore(unitOfWork: db => new RacingUnitOfWork(db, async () =>
        {
            if (fired) return;
            fired = true;
            await new ClosedTicketRepository(store!.ScopeFactory).SetAsync(Mark("a-1", by: "Grace"), default);
        }));
        store = racing;

        await new ClosedTicketRepository(racing.ScopeFactory).SetAsync(Mark("a-1", by: "Ada"), default);

        fired.Should().BeTrue();
        (await new ClosedTicketRepository(racing.ScopeFactory).OfAsync(Tracker, ["jira-a-1"], default))
            .Should().ContainSingle().Which.By.Should().Be("Ada", "the loser re-reads the winner's row and writes over it");
    }

    [Fact]
    public async Task ClosedTicketStore_ReopenBetweenInsertAndReRead_RetriesTheInsertOnce()
    {
        ServerStateStore? store = null;
        var steps = 0;
        using var racing = new ServerStateStore(unitOfWork: db => new ReopeningUnitOfWork(db, () => store!.Context(), () => steps++));
        store = racing;

        await new ClosedTicketRepository(racing.ScopeFactory).SetAsync(Mark("a-1", by: "Ada"), default);

        steps.Should().BeGreaterThan(2, "the first insert lost to a writer whose row a Reopen then deleted");
        (await new ClosedTicketRepository(racing.ScopeFactory).OfAsync(Tracker, ["jira-a-1"], default))
            .Should().ContainSingle().Which.By.Should().Be("Ada");
    }

    [Fact]
    public async Task ClosedTicketStore_Clear_AnswersWhetherARowWent()
    {
        await Store.SetAsync(Mark("a-1"), default);

        (await Store.ClearAsync(Tracker, "jira-a-1", default)).Should().BeTrue();
        (await Store.ClearAsync(Tracker, "jira-a-1", default)).Should().BeFalse();
        (await Store.OfAsync(Tracker, ["jira-a-1"], default)).Should().BeEmpty();
    }

    [Fact]
    public async Task ClosedTicketStore_OfAsync_ReturnsOnlyTheNamedKeysOfThatTracker()
    {
        await Store.SetAsync(Mark("a-1"), default);
        await Store.SetAsync(Mark("a-2"), default);
        await Store.SetAsync(Mark("a-3"), default);
        await Store.SetAsync(Mark("a-1", tracker: "other-tracker"), default);

        var marks = await Store.OfAsync(Tracker, ["jira-a-1", "jira-a-3", "jira-a-9"], default);

        marks.Select(m => (m.Tracker, m.TicketKey)).Should().Equal((Tracker, "jira-a-1"), (Tracker, "jira-a-3"));
        marks[0].ClosedPullRequests.Should().Equal("https://github.com/o/api/pull/a-1");
        (await Store.OfAsync(Tracker, [], default)).Should().BeEmpty();
    }

    public void Dispose() => _store.Dispose();

    // The first look finds nothing. Before the insert's save another writer stores the row (step 0);
    // the insert loses. At the re-read a Reopen has deleted it (step 1), so the insert is tried again (step 2).
    private sealed class ReopeningUnitOfWork(IUnitOfWork inner, Func<DbContext> other, Func<int> step) : IUnitOfWork
    {
        private int _sets;

        public DbSet<TEntity> Set<TEntity>() where TEntity : class
        {
            if (typeof(TEntity) == typeof(ClosedTicket) && ++_sets == 2)
            {
                using var db = other();
                db.Set<ClosedTicket>().ExecuteDelete();
                step();
            }
            return inner.Set<TEntity>();
        }

        public EntityEntry Add(object entity) => inner.Add(entity);

        public EntityEntry Update(object entity) => inner.Update(entity);

        public EntityEntry Remove(object entity) => inner.Remove(entity);

        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (step() == 0)
            {
                await using var db = other();
                db.Add(new ClosedTicket { Tracker = Tracker, TicketKey = "jira-a-1", TicketId = "A-1", Project = "sample", By = "Grace", ClosedAt = T });
                await db.SaveChangesAsync(cancellationToken);
            }
            return await inner.SaveChangesAsync(cancellationToken);
        }

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            inner.BeginTransactionAsync(cancellationToken);
    }
}
