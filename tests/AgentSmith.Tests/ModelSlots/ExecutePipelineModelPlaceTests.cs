using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Lifecycle;
using AgentSmith.Application.Services.Resume;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.Sandbox;
using AgentSmith.Tests.TestHelpers;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.ModelSlots;

/// <summary>
/// 2026-11-20-24bd: the place lives as long as the run executes. A second run on the same deployment waits at its
/// first model call until the first run's ExecuteAsync is left, then proceeds; a run that parks on a question gives
/// its place back on the way out.
/// </summary>
public sealed class ExecutePipelineModelPlaceTests
{
    private readonly ModelGateFixture _gate = new(); // no poll: only the release in ExecuteAsync wakes a waiter
    private readonly Mock<IPipelineExecutor> _pipeline = new();
    private readonly Mock<IConfigurationLoader> _config = new();
    private readonly AgentConfig _agent = ModelGateFixture.Agent(limit: 1);
    private readonly Dictionary<string, Func<PipelineContext, Task>> _bodies = new();

    public ExecutePipelineModelPlaceTests()
    {
        _config.Setup(c => c.LoadConfig("config.yml")).Returns(new AgentSmithConfig
        {
            Projects = { ["p"] = new ResolvedProject { Pipeline = "code", Repos = [new RepoConnection { Name = "p" }] } },
        });
        _pipeline.Setup(p => p.ExecuteAsync(
                It.IsAny<IReadOnlyList<string>>(), It.IsAny<ResolvedProject>(), It.IsAny<PipelineContext>(),
                It.IsAny<CancellationToken>()))
            .Returns<IReadOnlyList<string>, ResolvedProject, PipelineContext, CancellationToken>(async (_, _, ctx, _) =>
            {
                await _bodies[ctx.Get<string>(ContextKeys.RunId)](ctx);
                return CommandResult.Ok("done");
            });
    }

    [Fact]
    public async Task ASecondRunOnTheSameDeployment_WaitsUntilTheFirstRunsExecuteEnds_ThenProceeds()
    {
        var firstMayEnd = new TaskCompletionSource();
        var secondCalled = new TaskCompletionSource();
        _bodies["run-a"] = async _ =>
        {
            await _gate.CallAsync(_agent);
            await firstMayEnd.Task; // the run keeps executing, holding its place
        };
        _bodies["run-b"] = async _ =>
        {
            await _gate.CallAsync(_agent);
            secondCalled.TrySetResult();
        };

        var first = Run("run-a");
        await _gate.Provider.FirstArrived.Task.OrHang("run-a at the model");
        var second = Run("run-b");
        await TestWaits.UntilAsync(() => _gate.ReportedWaiting, "run-b reports that it waits");
        secondCalled.Task.IsCompleted.Should().BeFalse("run-a is still executing and holds the only place");

        firstMayEnd.SetResult();
        await first.OrHang("run-a ends");

        await secondCalled.Task.OrHang("run-b at the model after run-a ended");
        (await second.OrHang("run-b ends")).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ARunParkedOnAQuestion_FreesItsPlace()
    {
        _bodies["run-a"] = async ctx =>
        {
            await _gate.CallAsync(_agent);
            ctx.Set(ContextKeys.WaitingForInput, true);
        };

        await Run("run-a").OrHang("the parked run leaves ExecuteAsync");

        (await _gate.Slots.AcquireAsync(ModelRunSlotKey.For(_agent), 1, "run-b", CancellationToken.None))
            .Should().BeTrue("a parked run is not executing and holds no place");
        _gate.Signal.Holds("run-a", ModelRunSlotKey.For(_agent))
            .Should().BeFalse("a resumed run takes its place again at its first call");
        (await _gate.Slots.WaitedAsync(["run-a"], CancellationToken.None))
            .Should().BeEmpty("a resumed run's clocks start again, so no earlier wait is subtracted twice");
    }

    [Fact]
    public async Task ARunThatThrows_FreesItsPlace()
    {
        _bodies["run-a"] = async _ =>
        {
            await _gate.CallAsync(_agent);
            throw new InvalidOperationException("boom");
        };

        await Run("run-a").Invoking(t => t.OrHang("the failing run")).Should().ThrowAsync<InvalidOperationException>();

        (await _gate.Slots.AcquireAsync(ModelRunSlotKey.For(_agent), 1, "run-b", CancellationToken.None)).Should().BeTrue();
    }

    [Fact]
    public async Task ARunWhoseLeaseReleaseThrows_StillFreesItsPlace()
    {
        _bodies["run-a"] = async ctx =>
        {
            await _gate.CallAsync(_agent);
            ctx.Set(ContextKeys.WaitingForInput, true);
        };
        var lease = new Mock<IActiveRunLease>();
        lease.Setup(l => l.AttachRunAsync(It.IsAny<string>(), It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LeaseAttachOutcome.Attached);
        lease.Setup(l => l.ReleaseAsync(It.IsAny<string>(), It.IsAny<TicketId>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("lease store down"));

        var parked = Task.Run(() => UseCase(lease).ExecuteAsync(
            new PipelineRequest("p", "code", TicketId: new TicketId("7"), RunId: "run-a"), "config.yml", CancellationToken.None));
        await parked.Invoking(t => t.OrHang("the parked run with a failing lease release")).Should().ThrowAsync<InvalidOperationException>();

        (await _gate.Slots.AcquireAsync(ModelRunSlotKey.For(_agent), 1, "run-b", CancellationToken.None))
            .Should().BeTrue("the place goes first, in its own try, before anything that can throw");
    }

    private Task<CommandResult> Run(string runId) =>
        Task.Run(() => UseCase().ExecuteAsync(new PipelineRequest("p", "code", RunId: runId), "config.yml", CancellationToken.None));

    private ExecutePipelineUseCase UseCase(Mock<IActiveRunLease>? leaseMock = null)
    {
        var skills = new Mock<ISkillLoader>();
        skills.Setup(s => s.LoadVocabulary(It.IsAny<string>())).Returns(ConceptVocabulary.Empty);
        var lease = leaseMock ?? new Mock<IActiveRunLease>();
        var events = new AgentSmith.Tests.TestHelpers.RecordingEventPublisher();
        return new ExecutePipelineUseCase(
            _config.Object, Mock.Of<IIntentParser>(), _pipeline.Object,
            new ResumeRequestReader(new PipelineContextSerializer(NullLogger<PipelineContextSerializer>.Instance),
                Mock.Of<ICommandContextFactory>(f => f.Knows(It.IsAny<string>())), NullLogger<ResumeRequestReader>.Instance),
            new ResumedCapRecompute(new StubConfigResolver(), events, NullLogger<ResumedCapRecompute>.Instance),
            Mock.Of<ISourceConfigOverrider>(), new StubSkillsCatalogResolver(),
            new ConceptVocabularyLoader(new StubSkillsCatalogPath(), skills.Object, NullLogger<ConceptVocabularyLoader>.Instance),
            new PipelineConfigResolver(), events, _gate.RunContext, new ModelPricingResolver(),
            new RunCancellationRegistry(NullLogger<RunCancellationRegistry>.Instance), lease.Object,
            new RunHeartbeatPump(lease.Object, new NoOpRunHeartbeat(), TimeProvider.System, NullLogger<RunHeartbeatPump>.Instance),
            new StubConfigResolver(), Mock.Of<IProgressReporter>(), Mock.Of<IPipelineErrorHandler>(),
            NullLogger<ExecutePipelineUseCase>.Instance, new ModelPlaceRelease(NullLogger<ModelPlaceRelease>.Instance, _gate.Slots));
    }
}
