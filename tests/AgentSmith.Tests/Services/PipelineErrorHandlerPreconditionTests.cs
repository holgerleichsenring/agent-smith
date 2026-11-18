using AgentSmith.Application.Services;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Entities;
using AgentSmith.Application.Services.Lifecycle;
using AgentSmith.Application.Services.Persistence;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Services;

/// <summary>2026-11-16-f3fbb: a precondition stop comments on the ticket and moves nothing, so the ticket keeps
/// its tracker status and no unmoved record is written; every other failure finalizes as before.</summary>
public sealed class PipelineErrorHandlerPreconditionTests
{
    private readonly Mock<ITicketProvider> _tracker = new();
    private readonly Mock<IAsyncPipelineLifecycle> _lifecycle = new();
    private readonly InMemoryUnmovedTicketStore _unmoved = new();
    private readonly PipelineErrorHandler _sut;

    public PipelineErrorHandlerPreconditionTests()
    {
        var factory = new Mock<ITicketProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(_tracker.Object);
        _tracker.Setup(t => t.FinalizeAsync(It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TicketFinalizeResult.NotExpressible("Failed"));
        _sut = new PipelineErrorHandler(Mock.Of<ICommandExecutor>(), Mock.Of<ICommandContextFactory>(), factory.Object,
            new FailureTicketComment(), new UnmovedTicketReport(_unmoved, NullLogger<UnmovedTicketReport>.Instance),
            NullLogger<PipelineErrorHandler>.Instance);
    }

    [Theory]
    [InlineData(RunEndKinds.Preflight)]
    [InlineData(RunEndKinds.ImageUntrusted)]
    [InlineData(RunEndKinds.BootstrapMissing)]
    public async Task HandleStepFailure_PreconditionKind_CommentsAndMovesNothing(string kind)
    {
        await FailAsync(kind);

        _tracker.Verify(t => t.UpdateStatusAsync(It.Is<TicketId>(id => id.Value == "99"),
            It.Is<string>(s => s.Contains("<b>Error:</b> refused")), It.IsAny<CancellationToken>()), Times.Once);
        _tracker.Verify(t => t.FinalizeAsync(It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
        (await _unmoved.FindStandingAsync("p1", "99", "tracker-a", CancellationToken.None)).Should().BeNull();
        _lifecycle.Verify(l => l.MarkFailed(), Times.Once, "the attempt still ends Failed");
    }

    [Theory]
    [InlineData(null)]
    [InlineData(RunEndKinds.ReadinessUnknown)]
    [InlineData(RunEndKinds.CriteriaUnmet)]
    public async Task HandleStepFailure_OtherFailure_MovesTheTicketAsBefore(string? kind)
    {
        await FailAsync(kind);

        _tracker.Verify(t => t.FinalizeAsync(It.Is<TicketId>(id => id.Value == "99"), It.IsAny<string>(), "Failed",
            It.IsAny<CancellationToken>()), Times.Once);
        _tracker.Verify(t => t.UpdateStatusAsync(It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        (await _unmoved.FindStandingAsync("p1", "99", "tracker-a", CancellationToken.None)).Should().NotBeNull();
    }

    private Task FailAsync(string? kind)
    {
        var context = new PipelineContext();
        context.Set(ContextKeys.TicketId, new TicketId("99"));
        var project = FailedStatusProject.With("Failed", "p1", "tracker-a");
        return _sut.HandleStepFailureAsync([], project, context, _lifecycle.Object,
            CommandResult.Fail("refused") with { FailureKind = kind }, CancellationToken.None);
    }
}
