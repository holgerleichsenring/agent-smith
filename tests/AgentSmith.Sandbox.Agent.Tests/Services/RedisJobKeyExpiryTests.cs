using AgentSmith.Sandbox.Agent.Services;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;

namespace AgentSmith.Sandbox.Agent.Tests.Services;

/// <summary>
/// 2026-10-02-5ab2f: a server that dies never pops the results or reads the events, so the
/// agent's own writes carry the job-key expiry instead of living forever.
/// </summary>
[Collection(nameof(RedisJobBusCollection))]
public sealed class RedisJobKeyExpiryTests(RedisJobBusFixture fixture)
{
    [Fact]
    public async Task RedisJobBus_PushResult_ExpiresTheResultsKey()
    {
        var jobId = $"job-{Guid.NewGuid():N}";
        await using var bus = await Connect();

        await bus.PushResultAsync(jobId,
            new StepResult(StepResult.CurrentSchemaVersion, Guid.NewGuid(), 0, false, 1.0, null),
            CancellationToken.None);

        await AssertExpiresWithinADay(RedisKeys.ResultsKey(jobId));
    }

    [Fact]
    public async Task RedisEventChannel_Batch_ExpiresTheEventsKey()
    {
        var jobId = $"job-{Guid.NewGuid():N}";
        var bus = await Connect();

        bus.EnqueueEventsBatch(jobId, [new StepEvent(StepEvent.CurrentSchemaVersion, Guid.NewGuid(),
            StepEventKind.Stdout, "line", DateTimeOffset.UtcNow)]);
        await bus.DisposeAsync();

        await AssertExpiresWithinADay(RedisKeys.EventsKey(jobId));
    }

    private async Task AssertExpiresWithinADay(string key)
    {
        await using var raw = await ConnectionMultiplexer.ConnectAsync(fixture.ConnectionString);
        var ttl = await raw.GetDatabase().KeyTimeToLiveAsync(key);

        ttl.Should().NotBeNull("the key must not outlive a dead job");
        ttl!.Value.Should().BeGreaterThan(TimeSpan.FromHours(23)).And.BeLessThanOrEqualTo(RedisKeys.JobKeyTtl);
    }

    private async Task<RedisJobBus> Connect() =>
        await RedisJobBus.ConnectAsync(fixture.ConnectionString, NullLogger<RedisJobBus>.Instance, CancellationToken.None);
}
