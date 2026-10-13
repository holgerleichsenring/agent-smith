using AgentSmith.Application.Services.Events;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Events;
using AgentSmith.Server.Services.Events;
using AgentSmith.Tests.Server;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;

namespace AgentSmith.Tests.Tickets;

/// <summary>
/// 2026-10-13-9a59a: the ticket nudge rides the shared system stream so every replica drains it,
/// reaches the overview group, and never lands in the System page's ring, rollup or seed.
/// </summary>
public sealed class TicketActivityRoutingTests
{
    private static readonly DateTimeOffset T = DateTimeOffset.Parse("2026-10-13T09:00:00Z");
    private readonly FakeRedisStreams _redis = new();
    private readonly EventEnvelopeSerializer _envelopes = new();
    private readonly Mock<IRunEventFanout> _fanout = new();

    [Fact]
    public async Task SystemEventStream_TicketActivityChanged_GoesToOverview_NotRingNorRollup()
    {
        await PublishAsync(Nudge());
        var stream = new SystemEventStream(_fanout.Object, _envelopes);

        await stream.DrainAsync(Db, CancellationToken.None);

        _fanout.Verify(f => f.ToTicketActivityAsync(
            It.Is<TicketActivityChangedEvent>(e => e.Project == "alpha" && e.TicketKey == "github-42"),
            It.IsAny<CancellationToken>()), Times.Once);
        _fanout.Verify(f => f.ToSystemAsync(It.IsAny<SystemEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        _fanout.Verify(f => f.ToSystemActivityAsync(It.IsAny<SystemActivitySnapshot>(), It.IsAny<CancellationToken>()), Times.Never);
        stream.Recent.Should().BeEmpty();
    }

    [Fact]
    public async Task SystemEventStream_Rehydrate_SkipsTicketActivityChanged()
    {
        await PublishAsync(Nudge(), Config());
        var stream = new SystemEventStream(_fanout.Object, _envelopes);

        await stream.RehydrateAsync(Db, CancellationToken.None);

        stream.Recent.Should().ContainSingle().Which.Should().BeOfType<ConfigChangedEvent>();
    }

    [Fact]
    public async Task SystemBacklog_SkipsTicketActivityChanged()
    {
        await PublishAsync(Config(), Nudge());

        var backlog = await new SystemBacklogReader(_redis.Connection, _envelopes).ReadAsync();

        backlog.Should().ContainSingle().Which.Should().BeOfType<ConfigChangedEvent>();
    }

    [Fact]
    public void EventTypeResolver_ResolvesTicketActivityChanged()
    {
        EventTypeResolver.ResolveSystem(SystemEventType.TicketActivityChanged)
            .Should().Be(typeof(TicketActivityChangedEvent));
        ((int)SystemEventType.TicketActivityChanged).Should().Be(56);
    }

    [Fact]
    public async Task TicketActivityNudge_PublishesOnTheSystemStream()
    {
        var services = new ServiceCollection().AddLogging()
            .AddSingleton(_redis.Connection).AddSingleton(_envelopes)
            .AddSingleton<ISystemEventPublisher, RedisSystemEventPublisher>()
            .AddEventPublishing();
        await using var provider = services.BuildServiceProvider();

        await provider.GetRequiredService<ITicketActivityNudge>().ChangedAsync("alpha", "github-42", CancellationToken.None);

        var drained = new SystemEventStream(_fanout.Object, _envelopes);
        await drained.DrainAsync(Db, CancellationToken.None);
        _fanout.Verify(f => f.ToTicketActivityAsync(
            It.Is<TicketActivityChangedEvent>(e => e.TicketKey == "github-42"
                && e.Source == SystemStreamTicketActivityNudge.Source),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private IDatabase Db => _redis.Connection.GetDatabase();

    private async Task PublishAsync(params SystemEvent[] events)
    {
        var publisher = new RedisSystemEventPublisher(
            _redis.Connection, _envelopes, NullLogger<RedisSystemEventPublisher>.Instance);
        foreach (var e in events) await publisher.PublishAsync(e);
    }

    private static TicketActivityChangedEvent Nudge() => new("ticket-activity", "alpha", "github-42", T);

    private static ConfigChangedEvent Config() => new("config", 3, "person-a", T);
}
