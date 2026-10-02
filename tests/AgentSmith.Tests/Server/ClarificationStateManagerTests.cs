using AgentSmith.Server.Services;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PendingClarification = AgentSmith.Server.Models.PendingClarification;

namespace AgentSmith.Tests.Server;

/// <summary>
/// 2026-10-02-5ab2d: a chat command awaiting confirmation lives in the database with its expiry.
/// A take hands it out once — to one of two concurrent clicks — and never after its two hours.
/// </summary>
public sealed class ClarificationStateManagerTests
{
    private static readonly PendingClarification Fix = new("fix #58 in sample", "fix 58", "U1");

    [Fact]
    public async Task ClarificationStateManager_SetThenTake_ReturnsItOnce()
    {
        using var store = new ServerStateStore();
        var replicaA = Manager(store);
        await replicaA.SetAsync("Slack", "C1", Fix, CancellationToken.None);

        var taken = await Manager(store).TakeAsync("slack", "C1", CancellationToken.None);
        var again = await replicaA.TakeAsync("slack", "C1", CancellationToken.None);

        taken.Should().Be(Fix, "another replica reads the row the first one wrote");
        again.Should().BeNull("a take removes what it returns");
    }

    [Fact]
    public async Task ClarificationStateManager_Expired_TakeReturnsNull()
    {
        var clock = new SettableClock();
        using var store = new ServerStateStore();
        await Manager(store, clock).SetAsync("slack", "C1", Fix, CancellationToken.None);
        clock.Now += TimeSpan.FromHours(2);

        (await Manager(store, clock).TakeAsync("slack", "C1", CancellationToken.None)).Should().BeNull();
        await using var db = store.Context();
        (await db.Set<AgentSmith.Infrastructure.Persistence.Entities.PendingClarification>().CountAsync())
            .Should().Be(0, "the expired row is removed by the take that found it");
    }

    [Fact]
    public async Task ClarificationStateManager_SetTwice_KeepsTheLatestRequest()
    {
        using var store = new ServerStateStore();
        await Manager(store).SetAsync("slack", "C1", Fix, CancellationToken.None);
        await Manager(store).SetAsync("slack", "C1", Fix with { SuggestedText = "list tickets" }, CancellationToken.None);

        (await Manager(store).TakeAsync("slack", "C1", CancellationToken.None))!.SuggestedText.Should().Be("list tickets");
    }

    [Fact]
    public async Task ClarificationStateManager_TwoConcurrentTakes_OneWins()
    {
        using var store = new ServerStateStore(onDisk: true);
        for (var round = 0; round < 10; round++)
        {
            await Manager(store).SetAsync("slack", "C1", Fix, CancellationToken.None);

            var takes = await Task.WhenAll(
                Task.Run(() => Manager(store).TakeAsync("slack", "C1", CancellationToken.None)),
                Task.Run(() => Manager(store).TakeAsync("slack", "C1", CancellationToken.None)));

            takes.Count(t => t is not null).Should().Be(1, "a double click dispatches once");
        }
    }

    [Fact]
    public async Task ClarificationStateManager_LongTeamsConversationId_RoundTrips()
    {
        var conversation = "19:" + new string('a', 380) + "@thread.v2";
        using var store = new ServerStateStore();
        await Manager(store).SetAsync("teams", conversation, Fix, CancellationToken.None);

        (await Manager(store).TakeAsync("teams", conversation, CancellationToken.None)).Should().Be(Fix);
    }

    private static ClarificationStateManager Manager(ServerStateStore store, TimeProvider? clock = null) =>
        new(store.ScopeFactory, clock ?? TimeProvider.System, NullLogger<ClarificationStateManager>.Instance);
}
