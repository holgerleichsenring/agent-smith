using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Builders;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Tests.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Services;

/// <summary>
/// Parametrised across the new composed executor (PipelineExecutor) and the
/// pre-p0147e monolith (PipelineExecutorLegacy). Both must produce identical
/// observable outcomes; the parametrisation is the test-pack guarantee that
/// the migration is behaviour-preserving.
/// </summary>
public class PipelineExecutorTests
{
    public static IEnumerable<object[]> ExecutorShapes() => PipelineExecutorTestHarness.ExecutorShapes();

    [Theory]
    [MemberData(nameof(ExecutorShapes))]
    public async Task ExecuteAsync_AllCommandsSucceed_ReturnsOk(PipelineExecutorTestHarness.Shape shape)
    {
        var h = new PipelineExecutorTestHarness(shape);
        var emptyCommands = Array.Empty<string>();
        var project = new ResolvedProject();
        var pipeline = new PipelineContext();

        var result = await h.Sut.ExecuteAsync(emptyCommands, project, pipeline, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("Pipeline completed");
    }

    [Theory]
    [MemberData(nameof(ExecutorShapes))]
    public async Task ExecuteAsync_EmptyPipeline_ReturnsOk(PipelineExecutorTestHarness.Shape shape)
    {
        var h = new PipelineExecutorTestHarness(shape);
        var result = await h.Sut.ExecuteAsync(
            Array.Empty<string>(),
            new ResolvedProject(),
            new PipelineContext(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(ExecutorShapes))]
    public async Task ExecuteAsync_PostsWorkingStatusToTicket(PipelineExecutorTestHarness.Shape shape)
    {
        var h = new PipelineExecutorTestHarness(shape);
        var ticketProviderMock = new Mock<ITicketProvider>();
        h.TicketFactoryMock.Setup(f => f.Create(It.IsAny<TrackerConnection>()))
            .Returns(ticketProviderMock.Object);

        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.TicketId, new TicketId("42"));

        await h.Sut.ExecuteAsync(Array.Empty<string>(), new ResolvedProject(), pipeline, CancellationToken.None);

        ticketProviderMock.Verify(t => t.UpdateStatusAsync(
            It.Is<TicketId>(id => id.Value == "42"),
            It.Is<string>(s => s.Contains("working on")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [MemberData(nameof(ExecutorShapes))]
    public async Task ExecuteAsync_TicketStatusFailure_DoesNotBlockPipeline(PipelineExecutorTestHarness.Shape shape)
    {
        var h = new PipelineExecutorTestHarness(shape);
        h.TicketFactoryMock.Setup(f => f.Create(It.IsAny<TrackerConnection>()))
            .Throws(new Exception("Ticket provider unavailable"));

        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.TicketId, new TicketId("42"));

        var result = await h.Sut.ExecuteAsync(
            Array.Empty<string>(), new ResolvedProject(), pipeline, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(ExecutorShapes))]
    public async Task ExecuteAsync_FactoryThrows_ReturnsFail_DoesNotCrash(PipelineExecutorTestHarness.Shape shape)
    {
        var h = new PipelineExecutorTestHarness(shape);
        var commands = new[] { "BadCommand" };
        var project = new ResolvedProject();
        var pipeline = new PipelineContext();

        h.FactoryMock.Setup(f => f.Create(PipelineCommand.Simple("BadCommand"), project, pipeline))
            .Throws(new Exception("Unknown command: 'BadCommand'"));

        var result = await h.Sut.ExecuteAsync(commands, project, pipeline, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("BadCommand");
    }

    [Theory]
    [MemberData(nameof(ExecutorShapes))]
    public async Task ExecuteAsync_OperationCanceled_PropagatesException(PipelineExecutorTestHarness.Shape shape)
    {
        var h = new PipelineExecutorTestHarness(shape);
        var commands = new[] { "CancelledCommand" };
        var project = new ResolvedProject();
        var pipeline = new PipelineContext();

        h.FactoryMock.Setup(f => f.Create(PipelineCommand.Simple("CancelledCommand"), project, pipeline))
            .Throws(new OperationCanceledException());

        var act = async () => await h.Sut.ExecuteAsync(
            commands, project, pipeline, CancellationToken.None);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Theory]
    [MemberData(nameof(ExecutorShapes))]
    public async Task ExecuteAsync_PipelineFails_PostsHtmlFormattedFailureComment(
        PipelineExecutorTestHarness.Shape shape)
    {
        // Regression: failure comments were posted as raw markdown (## Agent Smith - Failed),
        // which AzDO's System.History rendered as plain text. HTML is the lingua franca:
        // AzDO interprets it directly and GitHub/GitLab markdown comments accept inline HTML.
        var h = new PipelineExecutorTestHarness(shape);
        var ticketProviderMock = new Mock<ITicketProvider>();
        h.TicketFactoryMock.Setup(f => f.Create(It.IsAny<TrackerConnection>()))
            .Returns(ticketProviderMock.Object);

        var commands = new[] { "BadCommand" };
        var project = new ResolvedProject();
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.TicketId, new TicketId("42"));

        h.FactoryMock.Setup(f => f.Create(PipelineCommand.Simple("BadCommand"), project, pipeline))
            .Throws(new Exception("Gate veto (Tester): coverage incomplete"));

        await h.Sut.ExecuteAsync(commands, project, pipeline, CancellationToken.None);

        ticketProviderMock.Verify(t => t.UpdateStatusAsync(
            It.Is<TicketId>(id => id.Value == "42"),
            It.Is<string>(s =>
                s.Contains("<b>Agent Smith — Failed</b>")
                && s.Contains("<b>Step:</b>")
                && s.Contains("<b>Error:</b>")
                && s.Contains("<br/>")
                && !s.Contains("## Agent Smith")
                && !s.Contains("**Step:**")),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
