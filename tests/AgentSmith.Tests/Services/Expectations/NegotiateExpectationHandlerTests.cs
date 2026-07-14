using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Expectations;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Expectations;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Services.Expectations;

/// <summary>p0328: headless runs auto-ratify but stamp 'unratified' on the run
/// expectation — visible degradation, never a silent skip.</summary>
public sealed class NegotiateExpectationHandlerTests
{
    private static readonly ExpectationDraft Draft = new(
        "Observed.", ["The fix holds."], [], null);

    [Fact]
    public async Task NegotiateExpectation_Headless_AutoRatifiesWithUnratifiedStamp()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.Headless, true);
        pipeline.Set(ContextKeys.RunId, "run-1");
        var events = new Mock<IEventPublisher>();
        RunEvent? published = null;
        events.Setup(e => e.PublishAsync(It.IsAny<RunEvent>(), It.IsAny<CancellationToken>()))
            .Callback<RunEvent, CancellationToken>((ev, _) => published = ev)
            .Returns(Task.CompletedTask);
        var handler = BuildHandler(events.Object, out var askGate);

        var result = await handler.ExecuteAsync(Context(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("unratified");
        var expectation = pipeline.Get<RatifiedExpectation>(ContextKeys.RunExpectation);
        expectation.Outcome.Should().Be(ExpectationOutcomes.Unratified);
        expectation.IsUnratified.Should().BeTrue();
        published.Should().BeOfType<ExpectationRatifiedEvent>()
            .Which.Outcome.Should().Be(ExpectationOutcomes.Unratified);
        askGate.Verify(g => g.AskAsync(
                It.IsAny<PipelineContext>(), It.IsAny<Contracts.Dialogue.DialogQuestion>(),
                It.IsAny<CancellationToken>()),
            Times.Never, "headless must never reach the dialogue ask gate");
    }

    [Fact]
    public async Task NegotiateExpectation_NoTicket_SkipsCleanly()
    {
        var handler = BuildHandler(Mock.Of<IEventPublisher>(), out _);
        var context = new NegotiateExpectationContext(
            Ticket: null, new AgentConfig(), Tracker: null, new PipelineContext());

        var result = await handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("skipped");
    }

    private static NegotiateExpectationHandler BuildHandler(
        IEventPublisher events, out Mock<IDialogueAskGate> askGate)
    {
        var drafter = new Mock<IExpectationDrafter>();
        drafter.Setup(d => d.DraftAsync(
                It.IsAny<Ticket>(), It.IsAny<AgentConfig>(), It.IsAny<PipelineContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Draft, (string?)null));
        askGate = new Mock<IDialogueAskGate>();
        return new NegotiateExpectationHandler(
            drafter.Object,
            new ExpectationQuestionBuilder(),
            new ExpectationRatifier(new ExpectationDraftValidator()),
            Mock.Of<IExpectationTrackerCommenter>(),
            new ExpectationOutcomeRecorder(
                events, NullLogger<ExpectationOutcomeRecorder>.Instance),
            askGate.Object,
            NullLogger<NegotiateExpectationHandler>.Instance);
    }

    private static NegotiateExpectationContext Context(PipelineContext pipeline) => new(
        new Ticket(new TicketId("7"), "Fix it", "It is broken", null, "Open", "test"),
        new AgentConfig(), Tracker: null, pipeline);
}
