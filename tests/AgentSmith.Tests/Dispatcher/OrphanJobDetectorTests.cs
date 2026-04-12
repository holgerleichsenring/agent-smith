using System.Runtime.CompilerServices;
using System.Text.Json;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services;
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
    private readonly Mock<IJobSpawner> _jobSpawner = new();
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
            _jobSpawner.Object,
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

    [Fact]
    public async Task DetectOrphans_ContainerAlive_DoesNotCleanup()
    {
        // Arrange: a tracked job with stale activity but container is still alive
        var state = new ConversationState
        {
            JobId = "alive-job",
            ChannelId = "C100",
            UserId = "U100",
            Platform = "slack",
            Project = "test",
            TicketId = 1,
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            LastActivityAt = DateTimeOffset.UtcNow.AddMinutes(-5)
        };
        SetupRedisState(state);
        await _listener.TrackJobAsync("alive-job", CancellationToken.None);

        _jobSpawner.Setup(s => s.IsAliveAsync("alive-job", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _sut.DetectAndCleanupOrphansAsync(CancellationToken.None);

        // Assert: no cleanup should have occurred
        _adapter.Verify(a => a.SendMessageAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DetectOrphans_ContainerDead_CleansUp()
    {
        // Arrange: a tracked job whose container is dead
        var state = new ConversationState
        {
            JobId = "dead-job",
            ChannelId = "C200",
            UserId = "U200",
            Platform = "slack",
            Project = "test",
            TicketId = 2,
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            LastActivityAt = DateTimeOffset.UtcNow.AddMinutes(-5)
        };
        SetupRedisState(state);
        await _listener.TrackJobAsync("dead-job", CancellationToken.None);

        _jobSpawner.Setup(s => s.IsAliveAsync("dead-job", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        await _sut.DetectAndCleanupOrphansAsync(CancellationToken.None);

        // Assert: cleanup should have occurred
        _adapter.Verify(a => a.SendMessageAsync(
            "C200", It.Is<string>(m => m.Contains("dead-job")), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DetectOrphans_YoungJob_SkipsEvenIfContainerDead()
    {
        // Arrange: a job started only 10 seconds ago (within MinRuntime grace)
        var state = new ConversationState
        {
            JobId = "young-job",
            ChannelId = "C300",
            UserId = "U300",
            Platform = "slack",
            Project = "test",
            TicketId = 3,
            StartedAt = DateTimeOffset.UtcNow.AddSeconds(-10),
            LastActivityAt = DateTimeOffset.UtcNow.AddSeconds(-5)
        };
        SetupRedisState(state);
        await _listener.TrackJobAsync("young-job", CancellationToken.None);

        _jobSpawner.Setup(s => s.IsAliveAsync("young-job", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        await _sut.DetectAndCleanupOrphansAsync(CancellationToken.None);

        // Assert: no cleanup — job is too young
        _adapter.Verify(a => a.SendMessageAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _jobSpawner.Verify(s => s.IsAliveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DetectOrphans_LivenessCheckFails_DoesNotCleanup()
    {
        // Arrange: container status check throws — should not declare orphaned
        var state = new ConversationState
        {
            JobId = "error-job",
            ChannelId = "C400",
            UserId = "U400",
            Platform = "slack",
            Project = "test",
            TicketId = 4,
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            LastActivityAt = DateTimeOffset.UtcNow.AddMinutes(-5)
        };
        SetupRedisState(state);
        await _listener.TrackJobAsync("error-job", CancellationToken.None);

        _jobSpawner.Setup(s => s.IsAliveAsync("error-job", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Docker socket unavailable"));

        // Act
        await _sut.DetectAndCleanupOrphansAsync(CancellationToken.None);

        // Assert: no cleanup — can't confirm container is dead
        _adapter.Verify(a => a.SendMessageAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private void SetupRedisState(ConversationState state)
    {
        var json = JsonSerializer.Serialize(state, JsonOptions);
        var channelKey = (RedisKey)$"conversation:{state.Platform}:{state.ChannelId}";
        var indexKey = (RedisKey)$"job-index:{state.JobId}";

        // GetByJobIdAsync: job-index:{jobId} → channel key → state JSON
        _db.Setup(d => d.StringGetAsync(indexKey, It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)channelKey.ToString());

        _db.Setup(d => d.StringGetAsync(channelKey, It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)json);

        // GetAllAsync: scan keys
        _server.Setup(s => s.KeysAsync(
                It.IsAny<int>(), It.IsAny<RedisValue>(), It.IsAny<int>(),
                It.IsAny<long>(), It.IsAny<int>(), It.IsAny<CommandFlags>()))
            .Returns(ToAsyncEnumerable(new[] { channelKey }));
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
