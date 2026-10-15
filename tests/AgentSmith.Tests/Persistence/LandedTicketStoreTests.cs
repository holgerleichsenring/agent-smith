using AgentSmith.Contracts.Tickets;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;

namespace AgentSmith.Tests.Persistence;

/// <summary>
/// 2026-10-14-dbceg: a ticket's landing is stored once per tracker connection and ticket key, over the
/// shipped migrations, read back by key, and outlives every run of the ticket.
/// </summary>
public sealed class LandedTicketStoreTests : IDisposable
{
    private const string Tracker = "sample-tracker";
    private static readonly DateTimeOffset T = new(2026, 10, 14, 9, 0, 0, TimeSpan.Zero);
    private readonly ServerStateStore _store = new();

    private LandedTicketRepository Store => new(_store.ScopeFactory);

    private static TicketLandingFact Landing(string ticket, string tracker = Tracker) =>
        new(tracker, $"jira-{ticket}", ticket.ToUpperInvariant(), "sample", TicketLandingKinds.Merged, T, null, null,
            $"https://github.com/o/api/pull/{ticket}");

    [Fact]
    public async Task LandedTicketStore_TryRecordTwice_InsertsOnce()
    {
        (await Store.TryRecordAsync(Landing("a-1"), default)).Should().BeTrue();
        (await Store.TryRecordAsync(Landing("a-1") with { Kind = TicketLandingKinds.ByHand }, default)).Should().BeFalse();

        (await Store.LandedAsync(Tracker, ["jira-a-1"], default)).Should().ContainSingle()
            .Which.Should().Be(Landing("a-1"), "the first landing stands");
    }

    [Fact]
    public async Task LandedTicketStore_LandedAsync_ReturnsOnlyTheNamedKeysOfThatTracker()
    {
        await Store.TryRecordAsync(Landing("a-1"), default);
        await Store.TryRecordAsync(Landing("a-2"), default);
        await Store.TryRecordAsync(Landing("a-3"), default);
        await Store.TryRecordAsync(Landing("a-1", tracker: "other-tracker"), default);

        var landed = await Store.LandedAsync(Tracker, ["jira-a-1", "jira-a-3", "jira-a-9"], default);

        landed.Select(l => (l.Tracker, l.TicketKey)).Should().Equal((Tracker, "jira-a-1"), (Tracker, "jira-a-3"));
        (await Store.LandedAsync(Tracker, [], default)).Should().BeEmpty();
    }

    [Fact]
    public async Task LandedTicket_DeletingEveryRunOfTheTicket_KeepsTheRow()
    {
        await Store.TryRecordAsync(Landing("a-1"), default);
        await using (var db = _store.Context())
        {
            db.Runs.AddRange(
                new Run { Id = "run-1", Project = "sample", Pipeline = "fix-bug", TicketId = "A-1" },
                new Run { Id = "run-2", Project = "sample", Pipeline = "fix-bug", TicketId = "A-1" });
            await db.SaveChangesAsync();
        }

        await using (var db = _store.Context())
            foreach (var run in new[] { "run-1", "run-2" }) await new RunDeletionRepository(db).DeleteAsync(run, default);

        await using (var db = _store.Context()) db.Runs.Should().BeEmpty();
        (await Store.LandedAsync(Tracker, ["jira-a-1"], default)).Should().ContainSingle();
    }

    public void Dispose() => _store.Dispose();
}
