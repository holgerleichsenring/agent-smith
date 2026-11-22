using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Lifecycle;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.UseCases;

/// <summary>
/// 2026-11-22-6d85c: a run the server's own stop broke off ends cancelled with the interrupted reason — the
/// infrastructure end Retry unchanged is offered for — instead of failed; without a stopping host every end stays.
/// </summary>
public sealed class ServerStopEndTests
{
    private readonly Mock<IConfigurationLoader> _config = new();
    private readonly Mock<IPipelineExecutor> _pipeline = new();
    private readonly Mock<IPipelineErrorHandler> _errors = new();
    private readonly TestHelpers.RecordingEventPublisher _events = new();
    private readonly CancellationTokenSource _stopping = new();

    public ServerStopEndTests()
    {
        _config.Setup(c => c.LoadConfig("config.yml")).Returns(new AgentSmithConfig
        {
            Projects = { ["todo-list"] = new ResolvedProject { Pipeline = "code", Repos = new[] { new RepoConnection { Name = "todo-list" } } } },
        });
    }

    [Fact]
    public async Task ExecuteAsync_ServerStopsMidRun_EndsCancelledAsInterrupted_AndDoesNotThrow()
    {
        using var host = new CancellationTokenSource();
        ThrowsOnceCancelled(host);
        _stopping.Cancel();
        host.Cancel();

        var result = await Sut(stopping: true).ExecuteAsync(Request(), "config.yml", host.Token);

        result.IsSuccess.Should().BeFalse();
        _events.Events.OfType<RunCancelRequestedEvent>().Should().ContainSingle().Which.Reason.Should().Be(OwedWorkCancel.Interrupted);
        var finished = _events.Events.OfType<RunFinishedEvent>().Single();
        finished.Status.Should().Be("cancelled");
        finished.Summary.Should().Be(ServerStopEnd.Summary);
        _errors.Verify(e => e.FinalizeFailedTicketAsync(It.IsAny<ResolvedProject>(), It.IsAny<PipelineContext>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never, "a lost process is not the ticket's failure");
    }

    [Fact]
    public async Task ExecuteAsync_CallerCancelledWithoutAStoppingServer_KeepsTheFailedEnd()
    {
        using var caller = new CancellationTokenSource();
        ThrowsOnceCancelled(caller);
        caller.Cancel();

        var act = () => Sut(stopping: true).ExecuteAsync(Request(), "config.yml", caller.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        _events.Events.OfType<RunFinishedEvent>().Single().Status.Should().Be("failed");
        _events.Events.OfType<RunCancelRequestedEvent>().Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_AFailureOtherThanTheCancellationWhileStopping_StaysFailed()
    {
        // The executor's fatal path already moved the ticket for it; the run says what the ticket says.
        _pipeline.Setup(p => p.ExecuteAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<ResolvedProject>(),
                It.IsAny<PipelineContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("redis went away"));
        _stopping.Cancel();

        var act = () => Sut(stopping: true).ExecuteAsync(Request(), "config.yml", CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _events.Events.OfType<RunFinishedEvent>().Single().Status.Should().Be("failed");
    }

    [Fact]
    public async Task ExecuteAsync_NoHost_KeepsTheFailedEnd()
    {
        using var caller = new CancellationTokenSource();
        ThrowsOnceCancelled(caller);
        caller.Cancel();

        var act = () => Sut(stopping: false).ExecuteAsync(Request(), "config.yml", caller.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        _events.Events.OfType<RunFinishedEvent>().Single().Status.Should().Be("failed");
    }

    private static PipelineRequest Request() => new("todo-list", "code", new TicketId("7"));

    private void ThrowsOnceCancelled(CancellationTokenSource source) =>
        _pipeline.Setup(p => p.ExecuteAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<ResolvedProject>(),
                It.IsAny<PipelineContext>(), It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromException<CommandResult>(new OperationCanceledException(source.Token)));

    private ExecutePipelineUseCase Sut(bool stopping)
    {
        var lifetime = new Mock<IHostApplicationLifetime>();
        lifetime.SetupGet(l => l.ApplicationStopping).Returns(_stopping.Token);
        var skills = new Mock<ISkillLoader>();
        skills.Setup(s => s.LoadVocabulary(It.IsAny<string>())).Returns(ConceptVocabulary.Empty);
        var lease = new Mock<IActiveRunLease>();
        return new ExecutePipelineUseCase(
            _config.Object, Mock.Of<IIntentParser>(), _pipeline.Object,
            new Application.Services.Resume.ResumeRequestReader(
                new Application.Services.Resume.PipelineContextSerializer(NullLogger<Application.Services.Resume.PipelineContextSerializer>.Instance),
                Mock.Of<ICommandContextFactory>(f => f.Knows(It.IsAny<string>())), NullLogger<Application.Services.Resume.ResumeRequestReader>.Instance),
            new Application.Services.Resume.ResumedCapRecompute(new Sandbox.StubConfigResolver(), _events,
                NullLogger<Application.Services.Resume.ResumedCapRecompute>.Instance),
            Mock.Of<ISourceConfigOverrider>(), new StubSkillsCatalogResolver(),
            new ConceptVocabularyLoader(new StubSkillsCatalogPath(), skills.Object, NullLogger<ConceptVocabularyLoader>.Instance),
            new PipelineConfigResolver(), _events, TestHelpers.EventTestStubs.RunContext, new ModelPricingResolver(),
            new RunCancellationRegistry(NullLogger<RunCancellationRegistry>.Instance), lease.Object,
            new RunHeartbeatPump(lease.Object, new NoOpRunHeartbeat(), TimeProvider.System, NullLogger<RunHeartbeatPump>.Instance),
            new Sandbox.StubConfigResolver(), Mock.Of<IProgressReporter>(), _errors.Object,
            NullLogger<ExecutePipelineUseCase>.Instance,
            serverStop: new ServerStopEnd(_events, TimeProvider.System, stopping ? lifetime.Object : null));
    }
}
