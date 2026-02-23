using System.Runtime.CompilerServices;
using System.Text.Json;
using AgentSmith.Dispatcher.Contracts;
using AgentSmith.Dispatcher.Models;
using AgentSmith.Dispatcher.Services;
using AgentSmith.Infrastructure.Models;
using AgentSmith.Infrastructure.Services.Bus;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;

namespace AgentSmith.Tests.Dispatcher;

public sealed class OrphanJobDetectorTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly Mock<IConnectionMultiplexer> _redis = new();
    private readonly Mock<IDatabase> _db = new();
    private readonly Mock<IServer> _server = new();
    private readonly Mock<IPlatformAdapter> _adapter = new();
    private readonly Mock<IMessageBus> _messageBus = new();
    private readonly ConversationStateManager _stateManager;
    private readonly MessageBusListener _listener;
    private readonly OrphanJobDetector _sut;

    public OrphanJobDetectorTests()
    {
        _redis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_db.Object);
        _redis.Setup(r => r.GetServers()).Returns([_server.Object]);
        _adapter.Setup(a => a.Platform).Returns("slack");

        // Mock SubscribeToJobAsync to return an async enumerable that blocks until cancelled
        _messageBus.Setup(m => m.SubscribeToJobAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string _, CancellationToken ct) => BlockForeverAsync(ct));

        _stateManager = new ConversationStateManager(
            _redis.Object,
            NullLogger<ConversationStateManager>.Instance);

        _listener = new MessageBusListener(
            _messageBus.Object,
            _stateManager,
            new[] { _adapter.Object },
            NullLogger<MessageBusListener>.Instance);

        _sut = new OrphanJobDetector(
            _stateManager,
            _listener,
            new[] { _adapter.Object },
            _messageBus.Object,
            NullLogger<OrphanJobDetector>.Instance);
    }

    [Fact]
    public async Task GetTrackedJobIds_ReturnsActiveJobs()
    {
        await _listener.TrackJobAsync("job-1", CancellationToken.None);
        await _listener.TrackJobAsync("job-2", CancellationToken.None);

        var jobIds = await _listener.GetTrackedJobIdsAsync(CancellationToken.None);

        jobIds.Should().Contain("job-1");
        jobIds.Should().Contain("job-2");
    }

    [Fact]
    public async Task CancelJobAsync_CancelsSubscription()
    {
        await _listener.TrackJobAsync("job-cancel", CancellationToken.None);

        var act = async () => await _listener.CancelJobAsync("job-cancel", CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task CancelJobAsync_NonExistentJob_DoesNotThrow()
    {
        var act = async () => await _listener.CancelJobAsync("nonexistent", CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsStatesFromRedis()
    {
        var state = new ConversationState
        {
            JobId = "stale-job",
            ChannelId = "C123",
            UserId = "U456",
            Platform = "slack",
            Project = "test-project",
            TicketId = 42,
            StartedAt = DateTimeOffset.UtcNow.AddHours(-1),
            LastActivityAt = DateTimeOffset.UtcNow.AddMinutes(-30)
        };

        var json = JsonSerializer.Serialize(state, JsonOptions);
        var key = (RedisKey)"conversation:slack:C123";

        _server.Setup(s => s.KeysAsync(
                It.IsAny<int>(), It.IsAny<RedisValue>(), It.IsAny<int>(),
                It.IsAny<long>(), It.IsAny<int>(), It.IsAny<CommandFlags>()))
            .Returns(ToAsyncEnumerable(new[] { key }));

        _db.Setup(d => d.StringGetAsync(key, It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)json);

        var allStates = await _stateManager.GetAllAsync(CancellationToken.None);

        allStates.Should().HaveCount(1);
        allStates[0].JobId.Should().Be("stale-job");
        allStates[0].ChannelId.Should().Be("C123");
    }

    [Fact]
    public async Task GetAllAsync_SkipsMalformedEntries()
    {
        var key = (RedisKey)"conversation:slack:C999";

        _server.Setup(s => s.KeysAsync(
                It.IsAny<int>(), It.IsAny<RedisValue>(), It.IsAny<int>(),
                It.IsAny<long>(), It.IsAny<int>(), It.IsAny<CommandFlags>()))
            .Returns(ToAsyncEnumerable(new[] { key }));

        _db.Setup(d => d.StringGetAsync(key, It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)"not-valid-json");

        var allStates = await _stateManager.GetAllAsync(CancellationToken.None);

        allStates.Should().BeEmpty();
    }

    private static async IAsyncEnumerable<BusMessage> BlockForeverAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Expected when cancelled
        }

        yield break;
    }

#pragma warning disable CS1998 // Async method lacks 'await' operators
    private static async IAsyncEnumerable<RedisKey> ToAsyncEnumerable(RedisKey[] keys)
#pragma warning restore CS1998
    {
        foreach (var key in keys)
            yield return key;
    }
}
