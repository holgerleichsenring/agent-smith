using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Persistence;

/// <summary>
/// 2026-10-02-5ab2e: the last webhook per platform in the database — read back per platform,
/// written at most once a minute per process, advisory on failure, safe against a concurrent
/// first insert.
/// </summary>
public sealed class DbWebhookDeliveryTrackerTests
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

    private bool _broken;

    [Fact]
    public async Task DbWebhookDeliveryTracker_Record_IsReadBackPerPlatform()
    {
        using var store = new ServerStateStore();
        await Tracker(store).RecordAsync("github", Noon);
        await Tracker(store).RecordAsync("jira", Noon.AddMinutes(1));

        var seen = await Tracker(store).GetLastSeenAsync();

        seen.Should().BeEquivalentTo(new Dictionary<string, DateTimeOffset>
        {
            ["github"] = Noon, ["jira"] = Noon.AddMinutes(1),
        }, "another replica reads what one replica wrote");
    }

    [Fact]
    public async Task DbWebhookDeliveryTracker_SecondRecordWithin60s_WritesNothing()
    {
        using var store = new ServerStateStore();
        var tracker = Tracker(store);
        await tracker.RecordAsync("github", Noon);

        await tracker.RecordAsync("github", Noon.AddSeconds(59));
        (await tracker.GetLastSeenAsync())["github"].Should().Be(Noon);
        await tracker.RecordAsync("github", Noon.AddSeconds(60));
        (await tracker.GetLastSeenAsync())["github"].Should().Be(Noon.AddSeconds(60));
    }

    [Fact]
    public async Task DbWebhookDeliveryTracker_DatabaseError_ReadsAsNeverSeen()
    {
        using var store = new ServerStateStore(unitOfWork: db => _broken ? throw new InvalidOperationException("down") : db);
        _broken = true;

        var record = () => Tracker(store).RecordAsync("github", Noon);

        await record.Should().NotThrowAsync("a display signal never fails a webhook");
        (await Tracker(store).GetLastSeenAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task DbWebhookDeliveryTracker_FailedWrite_DoesNotSetTheThrottleMark()
    {
        using var store = new ServerStateStore(unitOfWork: db => _broken ? throw new InvalidOperationException("down") : db);
        var tracker = Tracker(store);
        _broken = true;
        await tracker.RecordAsync("github", Noon);
        _broken = false;

        await tracker.RecordAsync("github", Noon.AddSeconds(5));

        (await tracker.GetLastSeenAsync())["github"].Should().Be(Noon.AddSeconds(5));
    }

    [Fact]
    public async Task DbWebhookDeliveryTracker_ConcurrentFirstInserts_BothLand()
    {
        var fired = false;
        DbWebhookDeliveryTracker? other = null;
        using var store = new ServerStateStore(unitOfWork: db => new RacingUnitOfWork(db, async () =>
        {
            if (fired) return;
            fired = true;
            await other!.RecordAsync("github", Noon);
        }));
        other = Tracker(store);

        await Tracker(store).RecordAsync("GitHub", Noon.AddMinutes(1));

        fired.Should().BeTrue();
        (await other.GetLastSeenAsync())["github"].Should().Be(Noon.AddMinutes(1), "the loser's insert became its update");
        await using var db = store.Context();
        (await db.Set<WebhookLastSeen>().CountAsync()).Should().Be(1);
    }

    private static DbWebhookDeliveryTracker Tracker(ServerStateStore store) =>
        new(store.ScopeFactory, NullLogger<DbWebhookDeliveryTracker>.Instance);
}
