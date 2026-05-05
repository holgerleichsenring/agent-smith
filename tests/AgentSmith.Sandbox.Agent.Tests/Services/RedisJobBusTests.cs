using System.Text.Json;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Sandbox.Agent.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;

namespace AgentSmith.Sandbox.Agent.Tests.Services;

[Collection(nameof(RedisJobBusCollection))]
public class RedisJobBusTests(RedisJobBusFixture fixture)
{
    [Fact]
    public async Task WaitForStepAsync_StepPushedByExternalProducer_IsReturned()
    {
        var jobId = NewJob();
        await using var bus = await Connect();
        var raw = await ConnectRaw();
        var step = MakeRunStep("echo", "hello");
        await raw.GetDatabase().ListLeftPushAsync(RedisKeys.InputKey(jobId),
            JsonSerializer.Serialize(step, WireFormat.Json));

        var received = await bus.WaitForStepAsync(jobId, TimeSpan.FromSeconds(5), CancellationToken.None);

        received.Should().NotBeNull();
        received!.Command.Should().Be("echo");
        received.Args.Should().Equal("hello");
    }

    [Fact]
    public async Task WaitForStepAsync_NoStepWithinTimeout_ReturnsNull()
    {
        var jobId = NewJob();
        await using var bus = await Connect();

        var step = await bus.WaitForStepAsync(jobId, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        step.Should().BeNull();
    }

    [Fact]
    public async Task EnqueueEventsBatch_ThenDispose_FlushesEventsToStream()
    {
        var jobId = NewJob();
        var bus = await Connect();
        var batch = new[] { MakeEvent("a"), MakeEvent("b"), MakeEvent("c") };
        bus.EnqueueEventsBatch(jobId, batch);
        await bus.DisposeAsync();

        var raw = await ConnectRaw();
        var entries = await raw.GetDatabase().StreamRangeAsync(RedisKeys.EventsKey(jobId));
        entries.Should().HaveCount(3);
    }

    [Fact]
    public async Task EnqueueEventsBatch_OverChannelCapacity_DropsOldestWithoutCrash()
    {
        var jobId = NewJob();
        var bus = await Connect();
        var oversized = Enumerable.Range(0, RedisEventChannel.Capacity + 200)
            .Select(i => MakeEvent($"line{i}")).ToArray();

        var act = () => { bus.EnqueueEventsBatch(jobId, oversized); return Task.CompletedTask; };
        await act.Should().NotThrowAsync();
        await bus.DisposeAsync();
    }

    [Fact]
    public async Task PushResultAsync_StoresResultAsListEntry()
    {
        var jobId = NewJob();
        await using var bus = await Connect();
        var result = new StepResult(StepResult.CurrentSchemaVersion, Guid.NewGuid(), 0, false, 1.5, null);

        await bus.PushResultAsync(jobId, result, CancellationToken.None);

        var raw = await ConnectRaw();
        var entries = await raw.GetDatabase().ListRangeAsync(RedisKeys.ResultsKey(jobId), 0, -1);
        entries.Should().HaveCount(1);
        var roundtripped = JsonSerializer.Deserialize<StepResult>(entries[0].ToString(), WireFormat.Json);
        roundtripped.Should().BeEquivalentTo(result);
    }

    private async Task<RedisJobBus> Connect() =>
        await RedisJobBus.ConnectAsync(fixture.ConnectionString, NullLogger<RedisJobBus>.Instance, CancellationToken.None);

    private async Task<IConnectionMultiplexer> ConnectRaw() =>
        await ConnectionMultiplexer.ConnectAsync(fixture.ConnectionString);

    private static string NewJob() => $"job-{Guid.NewGuid():N}";

    private static Step MakeRunStep(string command, params string[] args) =>
        new(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run, command, args, "/", null, 10);

    private static StepEvent MakeEvent(string line) =>
        new(StepEvent.CurrentSchemaVersion, Guid.NewGuid(), StepEventKind.Stdout, line, DateTimeOffset.UtcNow);
}
