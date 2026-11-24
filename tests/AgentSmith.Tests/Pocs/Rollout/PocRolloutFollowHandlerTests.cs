using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Pocs;
using AgentSmith.Application.Services.Pocs.Rollout;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.Pocs.Argo;
using AgentSmith.Tests.Pocs.Build;
using AgentSmith.Tests.Pocs.GitOps;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Pocs.Rollout;

/// <summary>2026-11-23-c81ec: the follow step — its deadline, what it writes on settle and on failure, and its lines.</summary>
public sealed class PocRolloutFollowHandlerTests
{
    private static readonly ArgoApplicationState Healthy = new("Synced", "Healthy", null, "Succeeded", null);
    private static readonly ArgoApplicationState Progressing = new("Synced", "Progressing", null, "Running", null);

    private readonly FakeArgoCd _argo = new();
    private readonly FakeRolloutStore _store = new();
    private readonly SkippingClock _clock = new();
    private readonly List<L1StepDetailEvent> _lines = [];
    private readonly FakeGitOpsStore _pocs = new()
    {
        Poc = new PocGitOpsSubject(7, "todo", "poc-ann", "main", 1, 2, PocRolloutVerdictTests.Digest, null, null, null),
    };

    [Fact]
    public async Task Execute_ThisRenderSettles_WritesHealthyAndReady_AndShowsEachReadingAsALine()
    {
        _argo.Readings.Add((Progressing, PocRolloutVerdictTests.Live(image: PocRolloutVerdictTests.OldImage)));
        _argo.Readings.Add((Healthy, PocRolloutVerdictTests.Live()));

        var result = await Handler().ExecuteAsync(Context(), default);

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Message.Should().Contain("Synced and Healthy").And.Contain("https://todo.poc.example.internal");
        _store.Outcomes.Should().ContainSingle().Which.Should().Match<PocRolloutOutcome>(o =>
            o.Revision == 2 && o.RolloutState == PocRolloutStates.Healthy && o.PocState == PocStates.Ready);
        _lines.Should().HaveCount(2).And.OnlyContain(l => l.Origin == PocRolloutLines.Origin);
        _argo.Refreshed.Should().Equal("poc-todo");
        _store.Commits.Should().Equal("c0ffee");
    }

    [Fact]
    public async Task Execute_DesiredZero_SettlesStopped()
    {
        _pocs.Poc = _pocs.Poc with { Replicas = 0 };
        _argo.Readings.Add((Healthy, PocRolloutVerdictTests.Live(replicas: 0, ready: 0, updated: 0)));

        (await Handler().ExecuteAsync(Context(), default)).IsSuccess.Should().BeTrue();

        _store.Outcomes.Single().PocState.Should().Be(PocStates.Stopped);
    }

    [Fact]
    public async Task Execute_AFirstRolloutThatFails_SetsThePocFailed()
    {
        _argo.Readings.Add((new ArgoApplicationState("Synced", "Degraded", "CrashLoopBackOff", null, null), PocRolloutVerdictTests.Live(ready: 0)));

        var result = await Handler().ExecuteAsync(Context(), default);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("CrashLoopBackOff").And.Contain("never ran");
        _store.Outcomes.Single().Should().Match<PocRolloutOutcome>(o => o.RolloutState == PocRolloutStates.Failed && o.PocState == PocStates.Failed);
    }

    [Fact]
    public async Task Execute_AFailureWithAnEarlierHealthyRevision_KeepsTheState()
    {
        _store.HealthyBefore = true;
        _argo.Readings.Add((new ArgoApplicationState("Synced", "Degraded", "CrashLoopBackOff", null, null), PocRolloutVerdictTests.Live(ready: 0)));

        var result = await Handler().ExecuteAsync(Context(), default);

        result.Message.Should().Contain("the previous version keeps serving (maxUnavailable 0)");
        _store.Outcomes.Single().PocState.Should().BeNull();
    }

    [Fact]
    public async Task Execute_TheRemainingWallTimeIsSmaller_IsTheDeadline_AndTheFailureNamesTheBudget()
    {
        _store.StartedAt = _clock.GetUtcNow().AddSeconds(-2500);
        _argo.Readings.Add((Progressing, PocRolloutVerdictTests.Live(ready: 0)));

        var result = await Handler().ExecuteAsync(Context(), default);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("pocs.run_wall_time_seconds (2700 s)").And.Contain("pocs.rollout.end_margin_seconds (60 s)")
            .And.Contain("never ran");
        _clock.Waits.Should().BeLessThanOrEqualTo(15, "2700 - 2500 - 60 = 140 s at 10 s per poll, not the 600 s deadline");
        _store.Outcomes.Single().RolloutState.Should().Be(PocRolloutStates.Failed);
    }

    [Fact]
    public async Task Execute_ModelWaitsDoNotCountAsSpentWallTime()
    {
        _store.StartedAt = _clock.GetUtcNow().AddSeconds(-2500);
        var slots = new Mock<IModelRunSlots>();
        slots.Setup(s => s.WaitedAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, TimeSpan> { ["run-1"] = TimeSpan.FromSeconds(2000) });
        _argo.Readings.Add((Progressing, PocRolloutVerdictTests.Live(ready: 0)));

        var result = await Handler(slots.Object).ExecuteAsync(Context(), default);

        result.Message.Should().Contain("pocs.rollout.deadline_seconds (600 s)", "the run has spent 500 s, so its own deadline is the smaller");
    }

    [Fact]
    public async Task Execute_ARetriedFollow_SettlesOnItsFirstPoll()
    {
        _argo.Readings.Add((Healthy, PocRolloutVerdictTests.Live()));

        (await Handler().ExecuteAsync(Context(), default)).IsSuccess.Should().BeTrue();

        (_argo.Polls, _clock.Waits).Should().Be((1, 0));
    }

    [Fact]
    public async Task Execute_ArgoRefusesTheToken_FailsAndRecordsNothing()
    {
        _argo.Refusal = new AgentSmith.Contracts.Exceptions.ArgoCdException("Argo CD https://argocd.poc.example.internal answered status 401 for api/v1/applications/poc-todo");

        var result = await Handler().ExecuteAsync(Context(), default);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("401").And.Contain("Nothing was recorded");
        _store.Outcomes.Should().BeEmpty();
    }

    private PocRolloutFollowContext Context()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.PocId, "7");
        pipeline.Set(ContextKeys.RunId, "run-1");
        pipeline.Set(ContextKeys.PocCommitSha, "c0ffee");
        pipeline.Set(ContextKeys.PocSeal, new PocSealResult(true, [], null, "sha256:c1"));
        return new PocRolloutFollowContext(pipeline, new ResolvedProject { Name = "poc-ann" }, PocSeeds.From(pipeline));
    }

    private PocRolloutFollowHandler Handler(IModelRunSlots? slots = null)
    {
        var config = new AgentSmithConfig { Pocs = PocsConfigTests.Sound() };
        var source = new PocsConfigSource(Mock.Of<IConfigurationLoader>(l => l.LoadConfig(It.IsAny<string>()) == config), new ServerContext("c.yml"));
        var events = new Mock<IEventPublisher>();
        events.Setup(e => e.PublishAsync(It.IsAny<RunEvent>(), It.IsAny<CancellationToken>()))
            .Callback<RunEvent, CancellationToken>((e, _) => { if (e is L1StepDetailEvent line) _lines.Add(line); }).Returns(Task.CompletedTask);
        var lines = new PocRolloutLines(events.Object, Mock.Of<IRunContextAccessor>(), NullLogger<PocRolloutLines>.Instance);
        return new PocRolloutFollowHandler(_pocs, _store, source, _argo, new PocRolloutExpectation(), new PocRolloutDeadline(_store, _clock, slots),
            new PocRolloutWatch(new PocRolloutVerdict(), lines, _clock, NullLogger<PocRolloutWatch>.Instance), new PocRolloutRecord(_store, _clock),
            NullLogger<PocRolloutFollowHandler>.Instance);
    }
}
