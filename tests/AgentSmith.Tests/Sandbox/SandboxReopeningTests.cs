using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.References;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// 2026-11-09-9631a: a sandbox removed under a turn costs one reopen, never a wait for the step
/// timeout, and what went does not come back through the hold.
/// </summary>
public sealed class SandboxReopeningTests
{
    private static readonly TimeSpan Quick = TimeSpan.FromMilliseconds(5);

    private readonly SourceScopeHoldFixture _fixture = new();
    private readonly FakeHeartbeat _heartbeat;

    public SandboxReopeningTests() => _heartbeat = new FakeHeartbeat(_fixture.Spawner);

    private ISourceScopeSandboxFactory Scopes() => _fixture.Scopes(
        Holds.Live(), new SandboxStepWatch(_heartbeat, NullLogger<SandboxStepWatch>.Instance, Quick));

    private static Step Read() => new(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.ReadFile, Path: "/work/a.cs");

    [Fact]
    public async Task SandboxStepWatch_HeartbeatGoneAndExitGone_ThrowsVanishedAndCancelsTheStep()
    {
        var sandbox = new ExitReadingSandbox(SandboxExitRead.Gone);
        var sut = new SandboxStepWatch(new FixedHeartbeat(false), NullLogger<SandboxStepWatch>.Instance, Quick);

        var act = () => sut.RunAsync(sandbox, Read(), null, CancellationToken.None).OrHang("the loss being noticed");

        await act.Should().ThrowAsync<SandboxVanishedException>();
    }

    [Fact]
    public async Task SandboxStepWatch_MissesButExitReadsRunning_KeepsWaiting()
    {
        var sandbox = new ExitReadingSandbox(SandboxExitRead.Running);
        var sut = new SandboxStepWatch(new FixedHeartbeat(false), NullLogger<SandboxStepWatch>.Instance, Quick);
        using var cts = new CancellationTokenSource();

        var run = sut.RunAsync(sandbox, Read(), null, cts.Token);
        (await TestWaits.StaysAsync(() => !run.IsCompleted)).Should().BeTrue("a Redis hiccup must not delete a live sandbox");
        await cts.CancelAsync();
        await run.Invoking(t => t).Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task SandboxStepWatch_LongStepWithLiveHeartbeat_ReturnsItsResult()
    {
        var probe = new FixedHeartbeat(true);
        var sandbox = new SlowSandbox(probe.AskedSeveralTimes);
        var sut = new SandboxStepWatch(probe, NullLogger<SandboxStepWatch>.Instance, Quick);

        var result = await sut.RunAsync(sandbox, Read(), null, CancellationToken.None).OrHang("the read recovering");

        result.ExitCode.Should().Be(0);
    }

    [Fact]
    public async Task SandboxStepWatch_NotHoldable_RunsWithoutProbing()
    {
        var probe = new FixedHeartbeat(false);
        var sut = new SandboxStepWatch(probe, NullLogger<SandboxStepWatch>.Instance, Quick);

        await sut.RunAsync(new PlainSandbox(), Read(), null, CancellationToken.None);

        probe.Asked.Should().Be(0);
    }

    [Fact]
    public async Task SourceScope_SandboxVanishes_ReopensOnceAndRetriesTheRead()
    {
        var scope = await _fixture.OpenAsync(Scopes());
        var first = _fixture.Spawner.Only;
        first.Vanished = true;

        var result = await scope.RunStepAsync(Read(), null, CancellationToken.None).OrHang("the read recovering");

        result.ExitCode.Should().Be(0);
        _fixture.Spawner.Spawned.Should().HaveCount(2);
        first.ForceRemovedAt.Should().NotBeNull();
        _fixture.Spawner.Spawned[1].Sandbox.RanSteps.Should().Contain(s => s.Kind == StepKind.ReadFile);
    }

    [Fact]
    public async Task SourceScope_ReopenFails_RefusesAndLaterReadsDoNotWait()
    {
        var scope = await _fixture.OpenAsync(Scopes());
        _fixture.Spawner.Only.Vanished = true;
        _fixture.Spawner.FailsFrom = 1;

        var first = await scope.RunStepAsync(Read(), null, CancellationToken.None).OrHang("the read recovering");
        var later = await scope.RunStepAsync(Read(), null, CancellationToken.None).OrHang("the read recovering");

        first.ErrorMessage.Should().Contain("could not be reopened").And.Contain("no capacity left");
        later.ErrorMessage.Should().Be(first.ErrorMessage);
        _fixture.Spawner.Spawned.Should().ContainSingle("a scope that failed to reopen spawns no more this turn");
        scope.IsMaterialized.Should().BeTrue("the record keeps a scope that was opened");
    }

    [Fact]
    public async Task SourceScope_SecondLossInATurn_RefusesWithoutAThirdSpawn()
    {
        var scope = await _fixture.OpenAsync(Scopes());
        _fixture.Spawner.Only.Vanished = true;
        await scope.RunStepAsync(Read(), null, CancellationToken.None).OrHang("the read recovering");
        _fixture.Spawner.Spawned[1].Sandbox.Vanished = true;

        var result = await scope.RunStepAsync(Read(), null, CancellationToken.None).OrHang("the read recovering");

        result.ExitCode.Should().NotBe(0);
        result.ErrorMessage.Should().Contain("could not be reopened");
        _fixture.Spawner.Spawned.Should().HaveCount(2);
    }

    [Fact]
    public async Task SourceScope_ParallelReads_ShareOneReopen()
    {
        var scope = await _fixture.OpenAsync(Scopes());
        _fixture.Spawner.Only.Vanished = true;

        var results = await Task.WhenAll(Enumerable.Range(0, 4)
            .Select(_ => scope.RunStepAsync(Read(), null, CancellationToken.None))).OrHang("the read recovering");

        results.Should().OnlyContain(r => r.ExitCode == 0);
        _fixture.Spawner.Spawned.Should().HaveCount(2);
    }

    [Fact]
    public async Task SourceScope_ReopenedSandbox_IsWhatTheHoldKeeps()
    {
        var scopes = Scopes();
        var scope = await _fixture.OpenAsync(scopes);
        _fixture.Spawner.Only.Vanished = true;
        await scope.RunStepAsync(Read(), null, CancellationToken.None).OrHang("the read recovering");
        await scope.DisposeAsync();

        var next = await _fixture.TurnAsync(scopes);

        next.JobId.Should().Be(_fixture.Spawner.Spawned[1].Sandbox.JobId);
        _fixture.Spawner.Spawned.Should().HaveCount(2);
    }

    [Fact]
    public async Task ReferenceSet_SandboxVanishes_RetriesReadButNotCommand()
    {
        var uploads = new ReferenceSandboxFixture();
        uploads.Set.Add(new ReferenceSetFile("a.txt", "hello"u8.ToArray()));
        var heartbeat = new FixedHeartbeat(false);
        var watch = new SandboxStepWatch(heartbeat, NullLogger<SandboxStepWatch>.Instance, Quick);
        var factory = uploads.Factory(Holds.Live(), watch);
        var read = factory.Create(ReferenceSandboxFixture.Project, ReferenceSandboxFixture.Conversation, "reference:a", "set-1");
        var command = factory.Create(ReferenceSandboxFixture.Project, ReferenceSandboxFixture.Conversation, "reference:b", "set-1");
        await read.MaterializeAsync(CancellationToken.None);
        await command.MaterializeAsync(CancellationToken.None);
        uploads.Spawned[0].Vanished = true;
        uploads.Spawned[1].Vanished = true;

        var answered = await read.RunStepAsync(
            new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.ReadFile, Path: "a.txt"), null, CancellationToken.None).OrHang("the read recovering");
        var refused = await command.RunStepAsync(
            new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run, Command: "/bin/sh"), null, CancellationToken.None).OrHang("the read recovering");

        answered.OutputContent.Should().Be("hello");
        refused.ExitCode.Should().NotBe(0);
        refused.ErrorMessage.Should().Contain("Set it up again");
        uploads.Spawned[3].Shells.Should().BeEmpty("a command that may rest on the lost container is not repeated");
    }

    [Fact]
    public async Task SourceScope_Reopen_ReportsReopeningThenReady()
    {
        var observer = new RecordingScopeObserver();
        using var observing = _fixture.Observers.Observe(observer);
        var scope = await _fixture.OpenAsync(Scopes());
        _fixture.Spawner.Only.Vanished = true;

        await scope.RunStepAsync(Read(), null, CancellationToken.None).OrHang("the read recovering");

        observer.Reports.Select(r => r.Item2).Should().Equal(
            SourceScopeProgress.Opening, SourceScopeProgress.Ready,
            SourceScopeProgress.Reopening, SourceScopeProgress.Ready);
    }

    [Fact]
    public async Task SourceScope_ReopenFails_ReportsReopeningThenFailed()
    {
        var observer = new RecordingScopeObserver();
        using var observing = _fixture.Observers.Observe(observer);
        var scope = await _fixture.OpenAsync(Scopes());
        _fixture.Spawner.Only.Vanished = true;
        _fixture.Spawner.FailsFrom = 1;

        await scope.RunStepAsync(Read(), null, CancellationToken.None).OrHang("the read refusing");

        observer.Reports.Select(r => r.Item2).Should().Equal(
            SourceScopeProgress.Opening, SourceScopeProgress.Ready,
            SourceScopeProgress.Reopening, SourceScopeProgress.Failed);
    }

    [Fact]
    public async Task ReferenceSet_Reopen_ReportsItsAddress()
    {
        var uploads = new ReferenceSandboxFixture();
        uploads.Set.Add(new ReferenceSetFile("a.txt", "hello"u8.ToArray()));
        var observers = new AsyncLocalSourceScopeObserverAccessor();
        var observer = new RecordingScopeObserver();
        using var observing = observers.Observe(observer);
        var scope = uploads.Factory(
                Holds.Live(), new SandboxStepWatch(new FixedHeartbeat(false), NullLogger<SandboxStepWatch>.Instance, Quick), observers)
            .Create(ReferenceSandboxFixture.Project, ReferenceSandboxFixture.Conversation, "reference:a", "set-1");
        await scope.MaterializeAsync(CancellationToken.None);
        observer.Reports.Should().BeEmpty("an upload draws no line for its first open");
        uploads.Spawned[0].Vanished = true;

        await scope.RunStepAsync(
            new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.ReadFile, Path: "a.txt"), null, CancellationToken.None)
            .OrHang("the read recovering");

        observer.Reports.Should().Equal(
            ("reference:a", SourceScopeProgress.Reopening), ("reference:a", SourceScopeProgress.Ready));
    }

    private sealed class FakeHeartbeat(FakeSandboxSpawner spawner) : ISandboxHeartbeatProbe
    {
        public Task<bool> IsAliveAsync(string jobId, CancellationToken cancellationToken) =>
            Task.FromResult(spawner.Spawned.Any(s => s.Sandbox.JobId == jobId && !s.Sandbox.Vanished));
    }

    private sealed class FixedHeartbeat(bool alive) : ISandboxHeartbeatProbe
    {
        private readonly TaskCompletionSource _several = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Asked { get; private set; }

        public Task AskedSeveralTimes => _several.Task;

        public Task<bool> IsAliveAsync(string jobId, CancellationToken cancellationToken)
        {
            if (++Asked >= 5) _several.TrySetResult();
            return Task.FromResult(alive);
        }
    }

    private static StepResult Done(Step step) => new(
        StepResult.CurrentSchemaVersion, step.StepId, 0, false, 0.01, null, string.Empty);

    private class PlainSandbox : ISandbox
    {
        public string JobId => "job-plain";

        public virtual Task<StepResult> RunStepAsync(
            Step step, IProgress<StepEvent>? progress, CancellationToken cancellationToken) =>
            Task.FromResult(Done(step));

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private class SlowSandbox(Task finishes) : PlainSandbox, IHoldableSandbox
    {
        public override async Task<StepResult> RunStepAsync(
            Step step, IProgress<StepEvent>? progress, CancellationToken cancellationToken)
        {
            await finishes.WaitAsync(cancellationToken);
            return Done(step);
        }

        public Task ForceRemoveAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class ExitReadingSandbox(SandboxExitRead exit)
        : SlowSandbox(new TaskCompletionSource().Task), ISandboxExitReader
    {
        public Task<SandboxExitRead> ReadExitAsync(CancellationToken cancellationToken) => Task.FromResult(exit);
    }
}
