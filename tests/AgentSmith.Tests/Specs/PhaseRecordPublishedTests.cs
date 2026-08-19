using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.PhaseExecution;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// p0466: the executed spec reaches the SERVER, not only the working tree. The tree copy
/// travels to the pull request and dies with the sandbox; a phase you can open after the
/// run needs a copy the server holds, and the event stream is the only channel a spawned
/// orchestrator has to it.
/// </summary>
public sealed class PhaseRecordPublishedTests
{
    private const string RunId = "2026-08-19T09-00-00-0001";

    [Fact]
    public async Task PhaseRecord_Written_IsAlsoAnnouncedForTheServerToHold()
    {
        var publisher = EventTestStubs.Recording();
        var pipeline = Pipeline();

        var result = await Handler(publisher).ExecuteAsync(Context(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var announced = publisher.Events.OfType<PhaseRecordedEvent>().Single();
        announced.RunId.Should().Be(RunId);
        announced.PhaseId.Should().Be("p19213a");
        announced.Body.Should().Contain("phase: p19213a");
    }

    /// <summary>
    /// An ordinary ticket carries no phase spec — there is nothing to record and nothing
    /// to announce, which is a different thing from an empty record.
    /// </summary>
    [Fact]
    public async Task PhaseRecord_RunWithoutAPhaseSpec_AnnouncesNothing()
    {
        var publisher = EventTestStubs.Recording();
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunId, RunId);

        await Handler(publisher).ExecuteAsync(Context(pipeline), CancellationToken.None);

        publisher.Events.Should().BeEmpty();
    }

    private static WritePhaseRecordHandler Handler(IEventPublisher publisher)
    {
        var files = new Mock<ISandboxFileReader>();
        var factory = new Mock<ISandboxFileReaderFactory>();
        factory.Setup(f => f.Create(It.IsAny<ISandbox>())).Returns(files.Object);
        return new WritePhaseRecordHandler(
            factory.Object,
            new ExecutedPhaseMarker(null!, NullLogger<ExecutedPhaseMarker>.Instance),
            publisher,
            new SandboxTargets(),
            NullLogger<WritePhaseRecordHandler>.Instance);
    }

    private static PipelineContext Pipeline()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunId, RunId);
        pipeline.Set(ContextKeys.PhaseSpec, new PhaseDraft(
            "p19213a", "Make the thing exist", "phase: p19213a\ngoal: \"Make the thing exist\"\n", []));
        pipeline.Set(ContextKeys.Sandbox, Mock.Of<ISandbox>());
        return pipeline;
    }

    private static WritePhaseRecordContext Context(PipelineContext pipeline) =>
        new(new Repository(new BranchName("main"), "https://example.invalid/sample.git"), pipeline);
}
