using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Turns;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Services.RateLimiting;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;

namespace AgentSmith.Tests.ModelSlots;

/// <summary>
/// 2026-11-20-24bd: the gate in the chat client stack. A run holds its place for every call it makes — four
/// sub-agents starting at once never wait — another run on the same deployment waits and says so once, a run on
/// another deployment does not, and a call outside any run is not gated.
/// </summary>
public sealed class ModelRunGateTests
{
    [Fact]
    public async Task OneRunWithLimitOne_FourSubAgentsStartAtOnce_NoneWaits()
    {
        var fixture = new ModelGateFixture(); // no poll: a sub-agent that waited would hang
        fixture.Provider.Hold = fixture.Provider.FourthArrived.Task; // each call returns only once all four are inside
        var agent = ModelGateFixture.Agent(limit: 1);

        using (fixture.RunContext.BeginScope("run-a"))
        {
            var subAgents = Enumerable.Range(0, 4).Select(_ => Task.Run(() => fixture.CallAsync(agent))).ToList();
            await Task.WhenAll(subAgents).OrHang("four sub-agents of one run inside the provider at once");
        }

        fixture.Provider.MostInsideAtOnce.Should().Be(4, "the master's place covers every sub-agent of the run");
        (await fixture.Slots.AcquireAsync(ModelRunSlotKey.For(agent), 1, "run-b", CancellationToken.None))
            .Should().BeFalse("run-a holds the one place");
        fixture.Events.Events.OfType<L1StepDetailEvent>().Should().BeEmpty("nobody waited");
    }

    [Fact]
    public async Task SecondRunOnTheSameDeployment_WaitsUntilTheFirstReleases_ThenProceeds()
    {
        var fixture = new ModelGateFixture();
        var agent = ModelGateFixture.Agent(limit: 1);
        using (fixture.RunContext.BeginScope("run-a")) await fixture.CallAsync(agent);

        Task second;
        using (fixture.RunContext.BeginScope("run-b")) second = fixture.CallAsync(agent);
        await TestWaits.UntilAsync(() => fixture.ReportedWaiting, "run-b reports that it waits");
        second.IsCompleted.Should().BeFalse("run-a holds the only place on the deployment");
        fixture.Provider.Arrived.Should().Be(1);
        fixture.Events.Events.OfType<LlmCallStartedEvent>().Where(e => e.RunId == "run-b")
            .Should().BeEmpty("nothing says a model call started while the run waits for its place");

        await fixture.Slots.ReleaseAsync("run-a", CancellationToken.None);

        await second.OrHang("run-b woken by the release (the fixture never polls)");
        fixture.Provider.Arrived.Should().Be(2);
    }

    [Fact]
    public async Task AFrameworkRun_PassesWhileAnotherRunHoldsTheOnePlace_AndTakesNone()
    {
        // 2026-11-22-9719a: a series beat is the framework's own short call; it never waits behind a coding run.
        var fixture = new ModelGateFixture();
        var agent = ModelGateFixture.Agent(limit: 1);
        using (fixture.RunContext.BeginScope("run-a")) await fixture.CallAsync(agent);

        fixture.Signal.Exempt("series-beat");
        using (fixture.RunContext.BeginScope("series-beat"))
            await fixture.CallAsync(agent).OrHang("the series beat passes although run-a holds the one place");

        fixture.Provider.Arrived.Should().Be(2);
        fixture.ReportedWaiting.Should().BeFalse("nobody waited");
        await fixture.Slots.ReleaseAsync("run-a", CancellationToken.None);
        (await fixture.Slots.AcquireAsync(ModelRunSlotKey.For(agent), 1, "run-c", CancellationToken.None))
            .Should().BeTrue("the series beat took no place, so the freed one is there");
    }

    [Fact]
    public async Task TwoAgentsOnDifferentDeployments_DoNotBlockEachOther()
    {
        var fixture = new ModelGateFixture();
        var first = ModelGateFixture.Agent(limit: 1, deployment: "gpt41");
        var second = ModelGateFixture.Agent(limit: 1, deployment: "gpt41-mini");
        ModelRunSlotKey.For(first).Should().NotBe(ModelRunSlotKey.For(second));
        using (fixture.RunContext.BeginScope("run-a")) await fixture.CallAsync(first);

        using (fixture.RunContext.BeginScope("run-b")) await fixture.CallAsync(second).OrHang("run-b on another deployment");

        fixture.Provider.Arrived.Should().Be(2);
    }

    [Fact]
    public async Task NoAmbientRunId_TheCallIsNotGated()
    {
        var fixture = new ModelGateFixture();
        var agent = ModelGateFixture.Agent(limit: 1);
        await fixture.Slots.AcquireAsync(ModelRunSlotKey.For(agent), 1, "run-other", CancellationToken.None);

        await fixture.CallAsync(agent).OrHang("an ungated call");

        fixture.Provider.Arrived.Should().Be(1, "a call outside any run takes no place and waits for none");
    }

    [Fact]
    public async Task AnAgentWithoutALimit_IsNotGated()
    {
        var fixture = new ModelGateFixture();
        var agent = ModelGateFixture.Agent(limit: null);
        await fixture.Slots.AcquireAsync(ModelRunSlotKey.For(agent), 1, "run-other", CancellationToken.None);

        using (fixture.RunContext.BeginScope("run-a")) await fixture.CallAsync(agent).OrHang("a call through an agent without a limit");

        fixture.Provider.Arrived.Should().Be(1);
    }

    [Fact]
    public async Task AWaitingRun_ShowsOneLine_OnTheRunAndOnTheTurn_NamingModelLimitAndKey()
    {
        var fixture = new ModelGateFixture(pollInterval: TimeSpan.FromMilliseconds(20));
        var agent = ModelGateFixture.Agent(limit: 1);
        var turn = new TurnActivityRecorder();
        using (fixture.RunContext.BeginScope("run-a")) await fixture.CallAsync(agent, task: TaskType.Scout);

        Task waiting;
        using (fixture.RunContext.BeginScope("run-b"))
        using (fixture.TurnActivity.Observe(turn))
        using (fixture.RunContext.BeginStepScope(3))
            waiting = fixture.CallAsync(agent);
        var asked = fixture.GateAsks;
        await TestWaits.UntilAsync(() => fixture.GateAsks >= asked + 5, "run-b polls the store several times");
        await fixture.Slots.ReleaseAsync("run-a", CancellationToken.None);
        await waiting.OrHang("run-b after the release");

        const string line = "Waiting for the model: gpt41 runs 1 at a time (agent.max_concurrent_runs)";
        turn.Seen.Where(a => a.Kind == TurnActivityKind.Waiting).Should().ContainSingle().Which.Detail.Should().Be(line);
        turn.Seen.Select(a => a.Kind).Should().ContainInOrder(TurnActivityKind.Waiting, TurnActivityKind.Thinking)
            .And.HaveCount(3, "waiting, then the call thinks only once it holds its place, then the model answers");
        var detail = fixture.Events.Events.OfType<L1StepDetailEvent>().Should().ContainSingle().Subject;
        detail.Should().BeEquivalentTo(new { RunId = "run-b", StepIndex = 3, Origin = ModelRunWaitLine.Origin, Detail = line });
    }

    [Fact]
    public async Task AWaitingCall_IsCancelledWithItsRun_AndTakesNoPlace()
    {
        var fixture = new ModelGateFixture();
        var agent = ModelGateFixture.Agent(limit: 1);
        using (fixture.RunContext.BeginScope("run-a")) await fixture.CallAsync(agent);
        using var cancel = new CancellationTokenSource();

        Task waiting;
        using (fixture.RunContext.BeginScope("run-b")) waiting = fixture.CallAsync(agent, cancel.Token);
        await TestWaits.UntilAsync(() => fixture.ReportedWaiting, "run-b reports that it waits");
        await cancel.CancelAsync();

        await waiting.Invoking(t => t.OrHang("the cancelled call")).Should().ThrowAsync<OperationCanceledException>();
        await fixture.Slots.ReleaseAsync("run-a", CancellationToken.None);
        (await fixture.Slots.AcquireAsync(ModelRunSlotKey.For(agent), 1, "run-c", CancellationToken.None))
            .Should().BeTrue("the cancelled run took nothing");
    }

    [Fact]
    public async Task ALimitBelowOne_IsRefusedAtTheCall_NamingTheKey()
    {
        var fixture = new ModelGateFixture();

        Func<Task> call;
        using (fixture.RunContext.BeginScope("run-a")) call = () => fixture.CallAsync(ModelGateFixture.Agent(limit: 0));

        using (fixture.RunContext.BeginScope("run-a"))
            (await call.Should().ThrowAsync<ConfigurationException>()).Which.Message
                .Should().Contain("max_concurrent_runs is 0").And.Contain(AgentConfig.MaxConcurrentRunsKey);
        fixture.Provider.Arrived.Should().Be(0);
    }

    [Fact]
    public async Task ARunHoldingAPlace_CallingAnotherAgent_TakesNoSecondPlace()
    {
        var fixture = new ModelGateFixture();
        var first = ModelGateFixture.Agent(limit: 1, deployment: "gpt41");
        var other = ModelGateFixture.Agent(limit: 1, deployment: "gpt41-mini");
        await fixture.Slots.AcquireAsync(ModelRunSlotKey.For(other), 1, "run-b", CancellationToken.None);
        using (fixture.RunContext.BeginScope("run-a")) await fixture.CallAsync(first);

        using (fixture.RunContext.BeginScope("run-a")) await fixture.CallAsync(other).OrHang("run-a through its second agent");

        fixture.Provider.Arrived.Should().Be(2, "a run holds one place, whichever agent it calls; no second place, no deadlock");
        await fixture.Slots.ReleaseAsync("run-a", CancellationToken.None);
        (await fixture.Slots.AcquireAsync(ModelRunSlotKey.For(first), 1, "run-c", CancellationToken.None)).Should().BeTrue();
    }

    [Fact]
    public async Task AHoldRemovedElsewhere_IsNoticedWithinTheConfirmWindow_AndTheRunWaitsAgain()
    {
        var fixture = new ModelGateFixture(pollInterval: TimeSpan.FromMilliseconds(20), storeSignal: new ModelRunSlotSignal());
        var agent = ModelGateFixture.Agent(limit: 1);
        using (fixture.RunContext.BeginScope("run-a")) await fixture.CallAsync(agent);
        await fixture.Slots.ReleaseAsync("run-a", CancellationToken.None); // deleted on another replica: this gate still trusts it
        await fixture.Slots.AcquireAsync(ModelRunSlotKey.For(agent), 1, "run-b", CancellationToken.None);
        fixture.Clock.Advance(ModelRunGate.ConfirmEvery);

        Task again;
        using (fixture.RunContext.BeginScope("run-a")) again = fixture.CallAsync(agent);
        await TestWaits.UntilAsync(() => fixture.ReportedWaiting, "run-a finds its place gone and waits");
        again.IsCompleted.Should().BeFalse();
        await fixture.Slots.ReleaseAsync("run-b", CancellationToken.None);

        await again.OrHang("run-a takes the freed place");
        fixture.Provider.Arrived.Should().Be(2);
    }

    [Fact]
    public async Task ALimitRaisedInTheConfiguration_LetsAWaitingRunIn()
    {
        var fixture = new ModelGateFixture(pollInterval: TimeSpan.FromMilliseconds(20));
        var agent = ModelGateFixture.Agent(limit: 1);
        fixture.Configured["azure"] = ModelGateFixture.Agent(limit: 1);
        using (fixture.RunContext.BeginScope("run-a")) await fixture.CallAsync(agent);

        Task waiting;
        using (fixture.RunContext.BeginScope("run-b")) waiting = fixture.CallAsync(agent);
        await TestWaits.UntilAsync(() => fixture.ReportedWaiting, "run-b waits at limit 1");
        fixture.Configured["azure"] = ModelGateFixture.Agent(limit: 2); // the studio raises it

        await waiting.OrHang("run-b admitted under the raised limit");
        fixture.Provider.Arrived.Should().Be(2);
    }

    [Fact]
    public async Task AWait_IsRecordedForTheWallTimeClocks_EvenWhenCancelled()
    {
        var fixture = new ModelGateFixture();
        var agent = ModelGateFixture.Agent(limit: 1);
        using (fixture.RunContext.BeginScope("run-a")) await fixture.CallAsync(agent);
        using var cancel = new CancellationTokenSource();

        Task waiting;
        using (fixture.RunContext.BeginScope("run-b")) waiting = fixture.CallAsync(agent, cancel.Token);
        await TestWaits.UntilAsync(() => fixture.ReportedWaiting, "run-b waits");
        fixture.Clock.Advance(TimeSpan.FromMinutes(40));
        (await fixture.Slots.WaitedAsync(["run-b"], CancellationToken.None))["run-b"]
            .Should().Be(TimeSpan.FromMinutes(40), "a wait still going on counts");
        await cancel.CancelAsync();
        await waiting.Invoking(t => t.OrHang("the cancelled wait")).Should().ThrowAsync<OperationCanceledException>();

        (await fixture.Slots.WaitedAsync(["run-b"], CancellationToken.None))["run-b"].Should().Be(TimeSpan.FromMinutes(40));
    }

    [Fact]
    public async Task EnterRunAsync_TakesThePlaceBeforeACallerStartsItsOwnClock()
    {
        var fixture = new ModelGateFixture();
        var agent = ModelGateFixture.Agent(limit: 1);

        using (fixture.RunContext.BeginScope("run-a")) await fixture.Factory.EnterRunAsync(agent, CancellationToken.None);

        fixture.Signal.Holds("run-a", ModelRunSlotKey.For(agent)).Should().BeTrue();
        fixture.Provider.Arrived.Should().Be(0, "entering takes the place without calling the model");
    }
}
