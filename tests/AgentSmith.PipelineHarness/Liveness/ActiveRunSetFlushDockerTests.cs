using AgentSmith.PipelineHarness.Presets;
using AgentSmith.Tests.Server;
using AgentSmith.Tests.TestHelpers;
using AgentSmith.Tests.TestSupport;
using Docker.DotNet;
using Docker.DotNet.Models;
using FluentAssertions;
using StackExchange.Redis;
using Xunit.Abstractions;

namespace AgentSmith.PipelineHarness.Liveness;

/// <summary>
/// 2026-10-02-5ab2c: a REAL Redis is flushed while a run lives. The server that comes up after
/// it finds nothing in Redis; the housekeeping re-seed — the real Lua script — puts the run
/// back into the active set from its fresh row, the broadcaster discovers it, and its
/// RunFinished reaches the row. The Redis is a container this test starts and removes, never
/// the operator's: a flush is destructive. Runs only when AGENTSMITH_HARNESS_DOCKER=1.
/// </summary>
[Trait("Category", "PipelineHarness")]
[Trait("Tier", "Docker")]
public sealed class ActiveRunSetFlushDockerTests(ITestOutputHelper output)
{
    private const string RedisImage = "redis:7-alpine";
    private const string RedisPort = "6379/tcp";

    [Fact]
    public async Task Flush_LiveRunAfterRealRedisFlush_RunFinishedReachesTheRow()
    {
        if (!DockerAvailability.IsAvailable(out var detail))
        {
            output.WriteLine(DockerAvailability.CoverageNotExercised + " (" + detail + ")");
            return;
        }
        using var docker = new DockerClientConfiguration(new Uri(
            Environment.GetEnvironmentVariable("DOCKER_HOST") ?? "unix:///var/run/docker.sock")).CreateClient();
        var containerId = await StartRedisAsync(docker);
        try
        {
            await using var redis = await ConnectAsync(docker, containerId);
            await ProveAsync(redis);
        }
        finally
        {
            await docker.Containers.RemoveContainerAsync(containerId, new ContainerRemoveParameters { Force = true });
        }
    }

    private static async Task ProveAsync(ConnectionMultiplexer redis)
    {
        using var harness = new WaitingRunHarness(redis);
        var first = harness.NewServerProcess();
        await first.StartAsync(CancellationToken.None);
        (await harness.StartAndAwaitDiscoveryAsync()).Should().BeTrue("the row must exist");
        await first.StopAsync(CancellationToken.None);

        await redis.GetServer(redis.GetEndPoints()[0]).FlushAllDatabasesAsync();
        var drain = harness.NewServerProcess(clock: new SettableClock()); // its database pass ran once, at start
        await drain.StartAsync(CancellationToken.None);
        (await harness.AwaitDrainPassesAsync(drain, 2)).Should().BeTrue();
        await harness.PublishGatesAsync(3); // the stream is re-created, without a RunStarted
        (await harness.IsInActiveSetAsync()).Should().BeFalse("the flush emptied the set");

        (await harness.NewReseeder().ReseedAsync(CancellationToken.None)).Should().Be(1);
        (await harness.IsInActiveSetAsync()).Should().BeTrue("the re-seed script adds the fresh row's run");
        (await TestWaits.ReachedAsync(() => drain.Active.ContainsKey(WaitingRunHarness.RunId)))
            .Should().BeTrue("the broadcaster discovers the re-seeded run");
        await harness.PublishFinishAsync("success");

        (await harness.AwaitFinishedAsync()).Should().BeTrue("RunFinished must reach the row");
        harness.RunStatus().Should().Be("success");
        (await harness.NewReseeder().ReseedAsync(CancellationToken.None)).Should().Be(0, "a finished run is not re-seeded");
        await drain.StopAsync(CancellationToken.None);
    }

    private static async Task<string> StartRedisAsync(IDockerClient docker)
    {
        var created = await docker.Containers.CreateContainerAsync(new CreateContainerParameters
        {
            Image = RedisImage,
            Name = "harness-5ab2c-redis-" + Guid.NewGuid().ToString("N")[..8],
            ExposedPorts = new Dictionary<string, EmptyStruct> { [RedisPort] = default },
            HostConfig = new HostConfig
            {
                PortBindings = new Dictionary<string, IList<PortBinding>>
                {
                    [RedisPort] = [new PortBinding { HostIP = "127.0.0.1", HostPort = "" }],
                },
            },
        });
        await docker.Containers.StartContainerAsync(created.ID, new ContainerStartParameters());
        return created.ID;
    }

    private static async Task<ConnectionMultiplexer> ConnectAsync(IDockerClient docker, string containerId)
    {
        var inspected = await docker.Containers.InspectContainerAsync(containerId);
        var port = inspected.NetworkSettings.Ports[RedisPort][0].HostPort;
        var options = ConfigurationOptions.Parse($"127.0.0.1:{port}");
        options.AllowAdmin = true; // FLUSHALL, on this test's own container only
        options.AbortOnConnectFail = false;
        var redis = await ConnectionMultiplexer.ConnectAsync(options);
        (await TestWaits.ReachedAsync(() => redis.IsConnected)).Should().BeTrue("the container's Redis must answer");
        return redis;
    }
}
