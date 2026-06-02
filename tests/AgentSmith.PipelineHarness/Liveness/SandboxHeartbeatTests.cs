using System.Diagnostics;
using AgentSmith.Application.Services.Lifecycle;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.PipelineHarness.Presets;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Server.Services.Sandbox;
using Docker.DotNet;
using Docker.DotNet.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;
using Xunit.Abstractions;

namespace AgentSmith.PipelineHarness.Liveness;

/// <summary>
/// p0201 docker-tier liveness coverage. Stands up a real DockerSandbox via
/// the production DockerSandboxFactory, plugs a SandboxLivenessWatcher onto
/// it, and exercises the three falsifiability anchors:
///   - LiveSandbox_HeartbeatPresent_NoCancel: green path, no cancel
///   - DeadSandbox_RunFailsWithin15s: `docker kill` mid-run, watcher cancels
///     under the 15s budget
///   - LiveSandbox_AgentGcPauseGreaterThanTtl_NoFalseCancel: pin the
///     dedicated-timer decision; a step that sleeps past the heartbeat TTL
///     must not trigger a false cancel because the timer runs on its own
///     thread
///   - LiveSandbox_RedisHiccupRecovers_NoFalseCancel: directly DELETE the
///     heartbeat key for &gt; 3 ticks while the container stays Running; the
///     watcher's docker-inspect probe must prevent a cancel
/// </summary>
[Trait("Category", "PipelineHarness")]
[Trait("Tier", "Docker")]
public sealed class SandboxHeartbeatTests(ITestOutputHelper output)
{
    private const string HarnessAgentImage = "agent-smith-sandbox-agent:latest";
    // The toolchain image runs /shared/agent (self-contained .NET 8 single-file
    // binary) as its main process. The binary needs glibc + libssl + libgcc;
    // Alpine's musl libc rejects it with "no such file or directory". The
    // dotnet runtime-deps image is the documented minimal glibc base and is
    // already on disk because the agent carrier image is built from it.
    private const string IdleToolchainImage = "mcr.microsoft.com/dotnet/runtime-deps:8.0-bookworm-slim";
    private static readonly TimeSpan WatcherDetectionBudget = TimeSpan.FromSeconds(15);
    // Mirrors HeartbeatLoop.KeyTtl (internal in AgentSmith.Sandbox.Agent).
    // 10s TTL + 4s slack so the synthetic GC-pause exceeds the TTL boundary.
    private const int HeartbeatTtlSeconds = 10;

    [Fact]
    public async Task LiveSandbox_HeartbeatPresent_NoCancel()
    {
        if (SkipIfUnavailable()) return;
        var (docker, multiplexer, factory, options) = await BuildBackendAsync();
        await using var fixture = await SandboxFixture.SpawnAsync(factory, "live-noop");
        await WaitForHeartbeatAsync(multiplexer, fixture.JobId, TimeSpan.FromSeconds(8));

        using var watcherFixture = new WatcherFixture(multiplexer, docker, fixture);
        watcherFixture.Start();

        await Task.Delay(TimeSpan.FromSeconds(8));

        watcherFixture.Registry.TryGetReason(fixture.RunId, out _).Should().BeFalse(
            "a live agent writing heartbeats must never trigger the watcher");
        watcherFixture.Publisher.Invocations
            .Any(i => i.Arguments[0] is SandboxVanishedEvent).Should().BeFalse();

        await CleanupAsync(multiplexer, options);
    }

    [Fact]
    public async Task DeadSandbox_RunFailsWithin15s()
    {
        if (SkipIfUnavailable()) return;
        var (docker, multiplexer, factory, options) = await BuildBackendAsync();
        await using var fixture = await SandboxFixture.SpawnAsync(factory, "kill-mid-run");
        await WaitForHeartbeatAsync(multiplexer, fixture.JobId, TimeSpan.FromSeconds(8));

        using var watcherFixture = new WatcherFixture(multiplexer, docker, fixture);
        watcherFixture.Start();

        await Task.Delay(TimeSpan.FromSeconds(2));
        output.WriteLine($"docker kill {fixture.ContainerId[..12]}");
        await docker.Containers.KillContainerAsync(fixture.ContainerId, new ContainerKillParameters());

        var sw = Stopwatch.StartNew();
        var cancelled = await WaitForCancelReasonAsync(
            watcherFixture.Registry, fixture.RunId, SandboxLivenessWatcher.CancelReason,
            WatcherDetectionBudget);
        sw.Stop();
        output.WriteLine($"cancel observed after {sw.Elapsed.TotalSeconds:F2}s");

        cancelled.Should().BeTrue(
            $"the watcher must signal cancel within {WatcherDetectionBudget.TotalSeconds:F0}s of docker-kill");
        sw.Elapsed.Should().BeLessThan(WatcherDetectionBudget,
            "explicit timing assertion for the falsifiability anchor");

        watcherFixture.Publisher.Invocations
            .Select(i => i.Arguments[0])
            .OfType<SandboxVanishedEvent>()
            .Should().NotBeEmpty("SandboxVanishedEvent must precede the cancel signal");

        await CleanupAsync(multiplexer, options);
    }

    [Fact]
    public async Task LiveSandbox_AgentGcPauseGreaterThanTtl_NoFalseCancel()
    {
        if (SkipIfUnavailable()) return;
        var (docker, multiplexer, factory, options) = await BuildBackendAsync();
        await using var fixture = await SandboxFixture.SpawnAsync(factory, "gc-pause");
        await WaitForHeartbeatAsync(multiplexer, fixture.JobId, TimeSpan.FromSeconds(8));

        using var watcherFixture = new WatcherFixture(multiplexer, docker, fixture);
        watcherFixture.Start();

        // Push a Run step that sleeps longer than HeartbeatLoop.KeyTtl. Because
        // the agent's heartbeat runs on a dedicated System.Threading.Timer, it
        // must keep refreshing the key on a thread-pool worker while the step
        // executor blocks. If the dedicated-timer decision regresses, this is
        // where it surfaces.
        var sleepSeconds = HeartbeatTtlSeconds + 4;
        var step = new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: "sleep", Args: [sleepSeconds.ToString()], WorkingDirectory: "/work", Env: null,
            TimeoutSeconds: sleepSeconds + 30);
        await PushStepAsync(multiplexer, fixture.JobId, step);

        await Task.Delay(TimeSpan.FromSeconds(sleepSeconds + 2));

        watcherFixture.Registry.TryGetReason(fixture.RunId, out _).Should().BeFalse(
            "a step that blocks past TTL must not trigger a cancel — the dedicated heartbeat timer keeps the key alive");

        await CleanupAsync(multiplexer, options);
    }

    [Fact]
    public async Task LiveSandbox_RedisHiccupRecovers_NoFalseCancel()
    {
        if (SkipIfUnavailable()) return;
        var (docker, multiplexer, factory, options) = await BuildBackendAsync();
        await using var fixture = await SandboxFixture.SpawnAsync(factory, "redis-hiccup");
        await WaitForHeartbeatAsync(multiplexer, fixture.JobId, TimeSpan.FromSeconds(8));

        using var watcherFixture = new WatcherFixture(multiplexer, docker, fixture);
        watcherFixture.Start();

        // Simulate a Redis hiccup: delete the heartbeat key repeatedly for ~6s
        // (>= MissThreshold * PollInterval). The container stays Running, so the
        // watcher's docker-inspect probe must clear the warning state without
        // cancelling. The agent will re-set the key every 2s; the explicit
        // delete keeps it missing for the duration of the hiccup window.
        var db = multiplexer.GetDatabase();
        var heartbeatKey = RedisKeys.HeartbeatKey(fixture.JobId);
        var hiccupEnd = DateTimeOffset.UtcNow.AddSeconds(6);
        while (DateTimeOffset.UtcNow < hiccupEnd)
        {
            await db.KeyDeleteAsync(heartbeatKey);
            await Task.Delay(300);
        }

        // Give the watcher a couple more ticks to re-check after the hiccup.
        await Task.Delay(TimeSpan.FromSeconds(6));

        watcherFixture.Registry.TryGetReason(fixture.RunId, out _).Should().BeFalse(
            "heartbeat-late with container Running must never cancel — the docker-inspect probe is the source of truth");

        await CleanupAsync(multiplexer, options);
    }

    private static async Task PushStepAsync(IConnectionMultiplexer multiplexer, string jobId, Step step)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(step, WireFormat.Json);
        await multiplexer.GetDatabase().ListLeftPushAsync(RedisKeys.InputKey(jobId), json);
    }

    private static async Task WaitForHeartbeatAsync(IConnectionMultiplexer multiplexer, string jobId, TimeSpan timeout)
    {
        var db = multiplexer.GetDatabase();
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (await db.KeyExistsAsync(RedisKeys.HeartbeatKey(jobId))) return;
            await Task.Delay(200);
        }
        throw new TimeoutException(
            $"Agent in job {jobId} never wrote a heartbeat within {timeout.TotalSeconds:F0}s");
    }

    private static async Task<bool> WaitForCancelReasonAsync(
        RunCancellationRegistry registry, string runId, string expectedReason, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (registry.TryGetReason(runId, out var reason) && reason == expectedReason) return true;
            await Task.Delay(100);
        }
        return false;
    }

    private static async Task<(IDockerClient, IConnectionMultiplexer, DockerSandboxFactory, DockerSandboxOptions)>
        BuildBackendAsync()
    {
        var options = new DockerSandboxOptions
        {
            RedisUrl = Environment.GetEnvironmentVariable("HARNESS_SANDBOX_REDIS_URL") ?? "redis:6379",
            DockerSocketUri = Environment.GetEnvironmentVariable("DOCKER_HOST") ?? "unix:///var/run/docker.sock",
            Network = Environment.GetEnvironmentVariable("HARNESS_SANDBOX_NETWORK")
                      ?? Environment.GetEnvironmentVariable("DOCKER_NETWORK") ?? "deploy_default",
        };
        var docker = new DockerClientConfiguration(new Uri(options.DockerSocketUri)).CreateClient();
        var hostRedisUrl = Environment.GetEnvironmentVariable("REDIS_URL") ?? "localhost:6379";
        var redisOpts = ConfigurationOptions.Parse(hostRedisUrl);
        redisOpts.AbortOnConnectFail = false;
        var multiplexer = await ConnectionMultiplexer.ConnectAsync(redisOpts);
        var loggerFactory = LoggerFactory.Create(b => b.SetMinimumLevel(LogLevel.Warning));
        var sandboxConfig = Options.Create(new SandboxGlobalConfig { StepTimeoutSeconds = 60 });
        var factory = new DockerSandboxFactory(
            docker, multiplexer,
            new DockerContainerSpecBuilder(),
            options, sandboxConfig, loggerFactory);
        return (docker, multiplexer, factory, options);
    }

    private static async Task CleanupAsync(IConnectionMultiplexer multiplexer, DockerSandboxOptions _)
    {
        try { await multiplexer.CloseAsync(); } catch { /* best effort */ }
    }

    private bool SkipIfUnavailable()
    {
        if (DockerAvailability.IsAvailable(out var detail)) return false;
        output.WriteLine(DockerAvailability.CoverageNotExercised + " (" + detail + ")");
        return true;
    }

    private sealed class WatcherFixture : IDisposable
    {
        public RunCancellationRegistry Registry { get; }
        public Mock<IEventPublisher> Publisher { get; } = new();
        private readonly SandboxLivenessWatcher _watcher;

        public WatcherFixture(IConnectionMultiplexer multiplexer, IDockerClient docker, SandboxFixture sandbox)
        {
            Registry = new RunCancellationRegistry(NullLogger<RunCancellationRegistry>.Instance);
            Registry.Register(sandbox.RunId, CancellationToken.None);
            Publisher.Setup(p => p.PublishAsync(It.IsAny<RunEvent>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _watcher = new SandboxLivenessWatcher(
                multiplexer, docker, Registry, Publisher.Object,
                new SandboxLivenessTarget(sandbox.RunId, sandbox.JobId, sandbox.ContainerId, sandbox.SandboxKey),
                NullLogger<SandboxLivenessWatcher>.Instance);
        }

        public void Start() => _watcher.Start();

        public void Dispose() => _watcher.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    private sealed class SandboxFixture : IAsyncDisposable
    {
        public ISandbox Sandbox { get; }
        public string JobId { get; }
        public string ContainerId { get; }
        public string RunId { get; }
        public string SandboxKey { get; } = "primary";

        private SandboxFixture(ISandbox sandbox, string jobId, string containerId, string runId)
        {
            Sandbox = sandbox;
            JobId = jobId;
            ContainerId = containerId;
            RunId = runId;
        }

        public static async Task<SandboxFixture> SpawnAsync(DockerSandboxFactory factory, string runSlug)
        {
            var runId = "harness-" + runSlug + "-" + Guid.NewGuid().ToString("N")[..8];
            var spec = new SandboxSpec(
                ToolchainImage: IdleToolchainImage,
                Resources: ResourceLimits.Default,
                AgentImage: HarnessAgentImage,
                RunId: runId);
            var sandbox = await factory.CreateAsync(spec, CancellationToken.None);
            var target = (ISandboxLivenessProbeTarget)sandbox;
            return new SandboxFixture(sandbox, sandbox.JobId, target.LivenessProbeTargetId, runId);
        }

        public ValueTask DisposeAsync() => Sandbox.DisposeAsync();
    }
}
