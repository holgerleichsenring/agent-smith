using AgentSmith.Application.Services.Claim;
using AgentSmith.Infrastructure.Services.Events;
using AgentSmith.PipelineHarness.Presets;
using AgentSmith.Server.Services.Sandbox;
using Docker.DotNet;
using Docker.DotNet.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;
using Xunit.Abstractions;

namespace AgentSmith.PipelineHarness.Liveness;

/// <summary>
/// p0201 docker-tier reaper coverage. Two falsifiability anchors:
///   - OrphanedContainer_ReaperRemovesIn35s: spawn a labelled container,
///     don't add it to ActiveRunsSet, wait past the 60s age rail, force a
///     scan, assert the container is gone.
///   - YoungContainer_ReaperLeavesAlone: spawn a labelled container,
///     immediately scan, assert the container survives the age rail.
/// Both run only when AGENTSMITH_HARNESS_DOCKER=1 AND docker is reachable.
/// </summary>
[Trait("Category", "PipelineHarness")]
[Trait("Tier", "Docker")]
public sealed class SandboxOrphanReaperTests(ITestOutputHelper output)
{
    private const string TestLabel = "agent-smith.job-id";
    private static readonly TimeSpan ContainerGracePadding = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task OrphanedContainer_ReaperRemovesIn35s()
    {
        if (SkipIfUnavailable()) return;
        var docker = ConnectDocker();
        var multiplexer = await ConnectRedisAsync();
        var jobId = "harness-orphan-" + Guid.NewGuid().ToString("N")[..8];
        var containerId = await SpawnLabelledIdleContainerAsync(docker, jobId);
        try
        {
            // Age-rail is 60s; wait past it plus padding so the scan sees the
            // container as eligible without racing the clock.
            output.WriteLine($"Waiting {SandboxOrphanReaper.MinContainerAge + ContainerGracePadding} for age rail");
            await Task.Delay(SandboxOrphanReaper.MinContainerAge + ContainerGracePadding);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var reaper = NewReaper(docker, multiplexer);
            await reaper.ScanOnceAsync(CancellationToken.None);
            sw.Stop();
            output.WriteLine($"ScanOnceAsync took {sw.Elapsed.TotalSeconds:F2}s");

            var stillExists = await ContainerExistsAsync(docker, containerId);
            stillExists.Should().BeFalse(
                "the reaper must force-remove a labelled container that is older than the age rail AND absent from ActiveRunsSet");
        }
        finally
        {
            await TryRemoveAsync(docker, containerId);
            await multiplexer.CloseAsync();
        }
    }

    [Fact]
    public async Task YoungContainer_ReaperLeavesAlone()
    {
        if (SkipIfUnavailable()) return;
        var docker = ConnectDocker();
        var multiplexer = await ConnectRedisAsync();
        var jobId = "harness-young-" + Guid.NewGuid().ToString("N")[..8];
        var containerId = await SpawnLabelledIdleContainerAsync(docker, jobId);
        try
        {
            // Immediate scan: the container is seconds old, well inside the
            // 60s age rail; the reaper must skip even though it's not in
            // ActiveRunsSet.
            var reaper = NewReaper(docker, multiplexer);
            await reaper.ScanOnceAsync(CancellationToken.None);

            var stillExists = await ContainerExistsAsync(docker, containerId);
            stillExists.Should().BeTrue(
                "the age rail must protect just-spawned containers from the reaper, closing the spawn-window race");
        }
        finally
        {
            await TryRemoveAsync(docker, containerId);
            await multiplexer.CloseAsync();
        }
    }

    private static SandboxOrphanReaper NewReaper(IDockerClient docker, IConnectionMultiplexer multiplexer)
    {
        var logger = NullLogger<SandboxOrphanReaper>.Instance;
        // p0242: a no-op lease => the DB active-run union is empty, so these
        // Redis/Docker-tier tests keep asserting the Redis-active-set behaviour.
        return new SandboxOrphanReaper(
            docker, multiplexer, new NoOpActiveRunLease(), logger);
    }

    private static async Task<string> SpawnLabelledIdleContainerAsync(IDockerClient docker, string jobId)
    {
        await EnsureImagePresentAsync(docker);
        var created = await docker.Containers.CreateContainerAsync(new CreateContainerParameters
        {
            Image = "alpine:3.20",
            Cmd = ["sleep", "600"],
            Labels = new Dictionary<string, string>
            {
                [TestLabel] = jobId
            },
            HostConfig = new HostConfig { AutoRemove = false }
        });
        await docker.Containers.StartContainerAsync(created.ID, new ContainerStartParameters());
        return created.ID;
    }

    private static async Task EnsureImagePresentAsync(IDockerClient docker)
    {
        try { await docker.Images.InspectImageAsync("alpine:3.20"); return; }
        catch (DockerImageNotFoundException) { /* fall through */ }
        await docker.Images.CreateImageAsync(
            new ImagesCreateParameters { FromImage = "alpine", Tag = "3.20" },
            authConfig: null, new Progress<JSONMessage>());
    }

    private static async Task<bool> ContainerExistsAsync(IDockerClient docker, string id)
    {
        try { await docker.Containers.InspectContainerAsync(id); return true; }
        catch (DockerContainerNotFoundException) { return false; }
    }

    private static async Task TryRemoveAsync(IDockerClient docker, string id)
    {
        try { await docker.Containers.RemoveContainerAsync(id, new ContainerRemoveParameters { Force = true }); }
        catch (DockerContainerNotFoundException) { /* already gone */ }
        catch { /* best effort */ }
    }

    private static IDockerClient ConnectDocker()
    {
        var uri = Environment.GetEnvironmentVariable("DOCKER_HOST") ?? "unix:///var/run/docker.sock";
        return new DockerClientConfiguration(new Uri(uri)).CreateClient();
    }

    private static Task<IConnectionMultiplexer> ConnectRedisAsync()
    {
        var url = Environment.GetEnvironmentVariable("REDIS_URL") ?? "localhost:6379";
        return ConnectAsync(url);
    }

    private static async Task<IConnectionMultiplexer> ConnectAsync(string url)
    {
        var opts = ConfigurationOptions.Parse(url);
        opts.AbortOnConnectFail = false;
        var mux = await ConnectionMultiplexer.ConnectAsync(opts);
        return mux;
    }

    private bool SkipIfUnavailable()
    {
        if (DockerAvailability.IsAvailable(out var detail)) return false;
        output.WriteLine(DockerAvailability.CoverageNotExercised + " (" + detail + ")");
        return true;
    }
}
