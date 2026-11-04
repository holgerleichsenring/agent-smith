using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-11-02-2b26d: an inline-ticket run has no ticket to park on. A standing review objection is asked in the
/// terminal and the default reading builds the reviewed specs; every hand-back whose answer the run cannot use
/// ends printing its question and readings instead of the park-status failure.
/// </summary>
public sealed class InlineHandbackAskTests
{
    private static readonly SpecHandback Objection = ReviewObjectionQuestion.For(
        [new StandingObjection("2026-11-02-1234a", [new SpecReviewFinding("The handler does not exist.", "src/H.cs:1")])]);

    private static readonly SpecHandback DerivationQuestion = new(
        SpecHandbackCase.Question, "'at 100' reads two ways",
        Readings: ["the boundary is included", "the boundary is excluded"], Taken: 0);

    [Fact]
    public async Task ReviewObjection_DefaultTaken_ClearsHandbackAndContinues()
    {
        var pipeline = Pipeline(Objection);

        var result = await Ask(Answering(ReviewObjectionQuestion.BuildAsReviewed)).HandleAsync(pipeline, Objection, default);

        result.IsSuccess.Should().BeTrue(result.Message);
        result.InsertNext.Should().BeNull("the rest of the preset builds the reviewed specs");
        pipeline.Has(ContextKeys.SpecHandback).Should().BeFalse();
        pipeline.Has(ContextKeys.OpenQuestionsAwaitingAnswer).Should().BeFalse("nothing parks");
    }

    [Fact]
    public async Task ReviewObjection_EmptyAnswer_TakesDefault()
    {
        var pipeline = Pipeline(Objection);

        var result = await Ask(Answering(string.Empty)).HandleAsync(pipeline, Objection, default);

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Message.Should().Contain("the default stands").And.Contain(ReviewObjectionQuestion.BuildAsReviewed);
        pipeline.Has(ContextKeys.SpecHandback).Should().BeFalse();
    }

    [Fact]
    public async Task ReviewObjection_ChangeAsAsked_FailsNamingTheChoice()
    {
        var pipeline = Pipeline(Objection);

        var result = await Ask(Answering(ReviewObjectionQuestion.ChangeAsAsked)).HandleAsync(pipeline, Objection, default);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain(ReviewObjectionQuestion.ChangeAsAsked).And.Contain("ticket text");
    }

    [Fact]
    public async Task DerivationQuestion_FailsPrintingQuestionAndReadings()
    {
        var gate = new Mock<IDialogueAskGate>();

        var result = await HandbackTestDoubles.Inline(gate.Object)
            .HandleAsync(Pipeline(DerivationQuestion), DerivationQuestion, default);

        result.IsSuccess.Should().BeFalse("a derivation hand-back carries no specs to build");
        result.Message.Should().Contain("'at 100' reads two ways")
            .And.Contain("(a) the boundary is included").And.Contain("(b) the boundary is excluded")
            .And.Contain("Run again with the answer in the ticket text")
            .And.NotContain("needs_clarification_status");
        gate.Verify(g => g.AskAsync(It.IsAny<PipelineContext>(), It.IsAny<DialogQuestion>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    internal static Mock<IDialogueAskGate> Answering(string answer, string by = "console-user")
    {
        var gate = new Mock<IDialogueAskGate>();
        gate.Setup(g => g.AskAsync(It.IsAny<PipelineContext>(), It.IsAny<DialogQuestion>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PipelineContext _, DialogQuestion q, CancellationToken _) =>
                DialogueAskOutcome.Answered(new DialogAnswer(q.QuestionId, answer, null, DateTimeOffset.UtcNow, by)));
        return gate;
    }

    private static InlineHandbackAsk Ask(Mock<IDialogueAskGate> gate) => HandbackTestDoubles.Inline(gate.Object);

    private static PipelineContext Pipeline(SpecHandback handback)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.InlineTicket, new InlineTicket("Fix the boundary", "Totals of 100.00 get it."));
        pipeline.Set(ContextKeys.SpecHandback, handback);
        return pipeline;
    }
}
