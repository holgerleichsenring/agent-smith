using System.Collections.Concurrent;
using AgentSmith.Contracts.Events;
using AgentSmith.Server.Services.Events;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services.Events;

/// <summary>
/// p0367: the run-event router is the volume-cut seam. A run-detail view with no
/// sandbox drawer open must receive lifecycle/meaning events (O(steps)) and NOT
/// the per-tool-call firehose (O(tool-calls)). These tests pin that routing plus
/// the back-pressure guard.
/// </summary>
public sealed class RunEventRouterTests
{
    private const string RunId = "2026-07-22T10-00-00-aaaa";
    private const string Repo = "default";

    private static RunEventRouter NewRouter(
        RecordingFanout fanout, SandboxExpansionRegistry registry, RecordingPersistence persistence) =>
        new(fanout, registry, new SandboxDetailEventClassifier(),
            new SandboxActivityCoalescer(), persistence);

    private static RunSnapshot Snapshot() => RunSnapshot.Empty(RunId);

    [Fact]
    public async Task Broadcaster_SandboxCommandResult_NotSentToRunGroup()
    {
        var fanout = new RecordingFanout();
        var router = NewRouter(fanout, new SandboxExpansionRegistry(), new RecordingPersistence());

        await router.DispatchAsync(RunId, Snapshot(),
            new SandboxCommandEvent(RunId, Repo, "dotnet", 4, DateTimeOffset.UtcNow), CancellationToken.None);
        await router.DispatchAsync(RunId, Snapshot(),
            new SandboxResultEvent(RunId, Repo, "dotnet", 0, 10, DateTimeOffset.UtcNow), CancellationToken.None);

        fanout.RunEvents.Should().BeEmpty();
        fanout.OverviewRunIds.Should().BeEmpty();
    }

    [Fact]
    public async Task Broadcaster_LifecycleEvent_SentToRunGroup()
    {
        var fanout = new RecordingFanout();
        var router = NewRouter(fanout, new SandboxExpansionRegistry(), new RecordingPersistence());

        await router.DispatchAsync(RunId, Snapshot(),
            new StepStartedEvent(RunId, 1, "build", 5, DateTimeOffset.UtcNow), CancellationToken.None);

        fanout.RunEvents.Should().ContainSingle().Which.Type.Should().Be(EventType.StepStarted);
        fanout.OverviewRunIds.Should().ContainSingle().Which.Should().Be(RunId);
    }

    [Fact]
    public async Task Broadcaster_SandboxDetail_OnlyToSandboxGroupWhenExpanded()
    {
        var fanout = new RecordingFanout();
        var registry = new SandboxExpansionRegistry();
        var router = NewRouter(fanout, registry, new RecordingPersistence());
        var command = new SandboxCommandEvent(RunId, Repo, "grep", 2, DateTimeOffset.UtcNow);

        await router.DispatchAsync(RunId, Snapshot(), command, CancellationToken.None);
        fanout.SandboxEvents.Should().BeEmpty("no drawer is open");

        registry.Expand(RunId, Repo);
        await router.DispatchAsync(RunId, Snapshot(), command, CancellationToken.None);
        fanout.SandboxEvents.Should().ContainSingle().Which.Type.Should().Be(EventType.SandboxCommand);
        fanout.RunEvents.Should().BeEmpty("detail never reaches the Run group");
    }

    [Fact]
    public async Task Persistence_ToolCallEvents_WrittenBatchedOffHotPath()
    {
        var fanout = new RecordingFanout();
        var persistence = new RecordingPersistence();
        var router = NewRouter(fanout, new SandboxExpansionRegistry(), persistence);

        await router.DispatchAsync(RunId, Snapshot(),
            new SandboxCommandEvent(RunId, Repo, "dotnet", 4, DateTimeOffset.UtcNow), CancellationToken.None);
        await router.DispatchAsync(RunId, Snapshot(),
            new SandboxResultEvent(RunId, Repo, "dotnet", 1, 10, DateTimeOffset.UtcNow), CancellationToken.None);

        // Kept for the tool-usage metrics (system-of-record) …
        persistence.Persisted.Select(e => e.Type)
            .Should().Contain(new[] { EventType.SandboxCommand, EventType.SandboxResult });
        // … but never on the broadcast Run-group send.
        fanout.RunEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task RunGroupTraffic_PerRun_IsOrderOfSteps_NotToolCalls()
    {
        var fanout = new RecordingFanout();
        var router = NewRouter(fanout, new SandboxExpansionRegistry(), new RecordingPersistence());

        // 3 steps, each wrapping 100 tool-call pairs — the p0367 shape in miniature.
        for (var step = 0; step < 3; step++)
        {
            await router.DispatchAsync(RunId, Snapshot(),
                new StepStartedEvent(RunId, step, "s", 3, DateTimeOffset.UtcNow), CancellationToken.None);
            for (var call = 0; call < 100; call++)
            {
                await router.DispatchAsync(RunId, Snapshot(),
                    new SandboxCommandEvent(RunId, Repo, "cmd", 1, DateTimeOffset.UtcNow), CancellationToken.None);
                await router.DispatchAsync(RunId, Snapshot(),
                    new SandboxResultEvent(RunId, Repo, "cmd", 0, 1, DateTimeOffset.UtcNow), CancellationToken.None);
            }
        }

        // 603 events dispatched; the Run group carried only the 3 steps.
        fanout.RunEvents.Should().HaveCount(3);
    }

    [Fact]
    public async Task Broadcaster_OneStalledClient_DoesNotBlockOthers()
    {
        var stall = new TaskCompletionSource();
        var inner = new StallingFanout(stallRunId: "slow", stall.Task);
        var options = new FanoutBackpressureOptions { SendTimeout = TimeSpan.FromMilliseconds(50) };
        var guarded = new BackpressureSafeFanout(inner, options, NullLogger<BackpressureSafeFanout>.Instance);

        var step = new StepStartedEvent("x", 1, "s", 1, DateTimeOffset.UtcNow);

        // The stalled client's send is abandoned past the timeout instead of hanging:
        // WaitAsync throws if the bounded send never returns (a generous guard so a
        // busy CI thread pool cannot flake it). The stall Task is never completed.
        var slow = guarded.ToRunAsync("slow", step, CancellationToken.None);
        await slow.WaitAsync(TimeSpan.FromSeconds(30));

        // … and a healthy client still gets its event.
        await guarded.ToRunAsync("fast", step, CancellationToken.None);
        inner.Delivered.Should().Contain("fast");

        stall.SetResult();
    }

    private sealed class RecordingFanout : IRunEventFanout
    {
        public ConcurrentQueue<RunEvent> RunEvents { get; } = new();
        public ConcurrentQueue<RunEvent> SandboxEvents { get; } = new();
        public ConcurrentQueue<string> OverviewRunIds { get; } = new();
        public ConcurrentQueue<SandboxActivityRollup> Rollups { get; } = new();

        public Task ToOverviewAsync(RunSnapshot snapshot, CancellationToken ct)
        { OverviewRunIds.Enqueue(snapshot.RunId); return Task.CompletedTask; }
        public Task ToRunAsync(string runId, RunEvent runEvent, CancellationToken ct)
        { RunEvents.Enqueue(runEvent); return Task.CompletedTask; }
        public Task ToSandboxAsync(string runId, string repo, RunEvent runEvent, CancellationToken ct)
        { SandboxEvents.Enqueue(runEvent); return Task.CompletedTask; }
        public Task ToRunActivityAsync(string runId, SandboxActivityRollup rollup, CancellationToken ct)
        { Rollups.Enqueue(rollup); return Task.CompletedTask; }
        public Task ToSystemAsync(SystemEvent systemEvent, CancellationToken ct) => Task.CompletedTask;
        public Task ToSystemActivityAsync(SystemActivitySnapshot snapshot, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class RecordingPersistence : IRunEventPersistence
    {
        public ConcurrentQueue<RunEvent> Persisted { get; } = new();
        public Task PersistAsync(RunEvent runEvent, CancellationToken ct)
        { Persisted.Enqueue(runEvent); return Task.CompletedTask; }
    }

    private sealed class StallingFanout(string stallRunId, Task stall) : IRunEventFanout
    {
        public ConcurrentQueue<string> Delivered { get; } = new();

        public Task ToRunAsync(string runId, RunEvent runEvent, CancellationToken ct)
        {
            if (runId == stallRunId) return stall;
            Delivered.Enqueue(runId);
            return Task.CompletedTask;
        }

        public Task ToOverviewAsync(RunSnapshot snapshot, CancellationToken ct) => Task.CompletedTask;
        public Task ToSandboxAsync(string runId, string repo, RunEvent runEvent, CancellationToken ct) => Task.CompletedTask;
        public Task ToRunActivityAsync(string runId, SandboxActivityRollup rollup, CancellationToken ct) => Task.CompletedTask;
        public Task ToSystemAsync(SystemEvent systemEvent, CancellationToken ct) => Task.CompletedTask;
        public Task ToSystemActivityAsync(SystemActivitySnapshot snapshot, CancellationToken ct) => Task.CompletedTask;
    }
}
