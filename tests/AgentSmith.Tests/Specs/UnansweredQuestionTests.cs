using AgentSmith.Application.Services.Prompts;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-09-07-c9d4: a question nobody answered becomes the derivation's answer. The
/// signal is the CONVERSATION — a comment by anyone but us after our question — never
/// the branch, because every derivation commits a fresh revision and a sha comparison
/// would call every re-trigger progress. The pin names the taken reading from the
/// question that was persisted, and the notice tells the ticket the same reading.
/// </summary>
public sealed class UnansweredQuestionTests
{
    private const string ReadingA = "a major only where nothing lower clears the advisory";
    private const string ReadingB = "the newest major everywhere, breaking changes included";

    private static readonly DateTimeOffset Asked = new(2026, 9, 7, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Pin_QuestionWithNoCommentAtAll_PinsTheTakenReading()
    {
        var pipeline = new PipelineContext();

        var unanswered = new UnansweredQuestionPin(NullLogger<UnansweredQuestionPin>.Instance)
            .Pin(Previous(taken: 1), pipeline);

        unanswered.Should().NotBeNull();
        unanswered!.TakenLabel.Should().Be("(b)");
        unanswered.TakenReading.Should().Be(ReadingB);
        var pin = pipeline.Get<string>(ContextKeys.SpecQuestionPin);
        pin.Should().Contain("(a) " + ReadingA).And.Contain("(b) " + ReadingB);
        pin.Should().Contain("Reading (b) is").And.Contain("Do not ask this question again");
    }

    [Fact]
    public void Pin_OurQuestionIsTheLastWord_PinsTheTakenReading()
    {
        var pipeline = PipelineWith(
            Comment("operator", Asked.AddHours(-3), "please also bump the test packages"),
            OurQuestion(Asked));

        new UnansweredQuestionPin(NullLogger<UnansweredQuestionPin>.Instance)
            .Pin(Previous(taken: 0), pipeline)
            .Should().NotBeNull("a comment BEFORE our question does not answer it");
        pipeline.Has(ContextKeys.SpecQuestionPin).Should().BeTrue();
    }

    [Fact]
    public void Pin_AnOperatorAnsweredAfterOurQuestion_PinsNothing()
    {
        var pipeline = PipelineWith(
            OurQuestion(Asked),
            Comment("operator", Asked.AddHours(1), "(b) — go to the newest major everywhere"));

        new UnansweredQuestionPin(NullLogger<UnansweredQuestionPin>.Instance)
            .Pin(Previous(taken: 0), pipeline)
            .Should().BeNull("the answer is in the conversation; the derivation reads it there");
        pipeline.Has(ContextKeys.SpecQuestionPin).Should().BeFalse();
    }

    [Fact]
    public void Pin_OnlyOurOwnNoticesFollowTheQuestion_StillPins()
    {
        var pipeline = PipelineWith(
            OurQuestion(Asked),
            Comment("agent-smith", Asked.AddHours(1), "## Agent Smith — Cancelled\n\nCancelled by operator."));

        new UnansweredQuestionPin(NullLogger<UnansweredQuestionPin>.Instance)
            .Pin(Previous(taken: 0), pipeline)
            .Should().NotBeNull("our own run notices are not an answer");
    }

    [Fact]
    public void Pin_OthersCommentedButOurQuestionIsNotInTheThread_PinsNothing()
    {
        var pipeline = PipelineWith(Comment("operator", Asked, "see the attached advisory"));

        new UnansweredQuestionPin(NullLogger<UnansweredQuestionPin>.Instance)
            .Pin(Previous(taken: 0), pipeline)
            .Should().BeNull("without our question in the thread nothing orders the comments — the safe direction is to derive on them");
    }

    [Theory]
    [InlineData(SpecHandbackCase.RequirementsContradictRepository)]
    [InlineData(SpecHandbackCase.NotImplementable)]
    public void Pin_PreviousHandbackIsNotAQuestion_PinsNothing(SpecHandbackCase other)
    {
        var previous = Previous(taken: 0) with { Handback = new SpecHandback(other, "reason") };
        var pipeline = new PipelineContext();

        new UnansweredQuestionPin(NullLogger<UnansweredQuestionPin>.Instance)
            .Pin(previous, pipeline).Should().BeNull();
        pipeline.Has(ContextKeys.SpecQuestionPin).Should().BeFalse();
    }

    [Fact]
    public void Pin_NoPreviousSet_PinsNothing() =>
        new UnansweredQuestionPin(NullLogger<UnansweredQuestionPin>.Instance)
            .Pin(null, new PipelineContext()).Should().BeNull();

    [Fact]
    public void Compose_PinnedQuestion_ReachesTheDerivationPromptWithoutAPreviousCutSection()
    {
        var pipeline = new PipelineContext();
        var previous = Previous(taken: 0);
        new UnansweredQuestionPin(NullLogger<UnansweredQuestionPin>.Instance).Pin(previous, pipeline);

        var prompt = SpecPromptComposer.Compose(
            Ticket(), TicketSegmenter.Segment("Adopt the newest versions, even breaking."),
            previous, SpecRevisionCause.Retrigger, pipeline);

        prompt.Should().Contain("## The question from the last run was left unanswered");
        prompt.Should().Contain("Reading (a) is");
        prompt.Should().NotContain("The previous cut — AMEND it", "a hand-back holds no cut to amend");
    }

    [Fact]
    public async Task Notice_ProceedingOnAReading_NamesItFromThePersistedQuestion()
    {
        var tickets = new Mock<ITicketProvider>();
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.Ticket, Ticket());
        var unanswered = new UnansweredQuestion(Previous(taken: 1).Handback!, "(b)", ReadingB);

        await Notice(tickets).PostAsync(pipeline, Tracker(), unanswered, default);

        tickets.Verify(t => t.UpdateStatusAsync(
            It.IsAny<TicketId>(),
            It.Is<string>(c => c.Contains("proceeding on reading (b)") && c.Contains("> (b) " + ReadingB)),
            It.IsAny<CancellationToken>()), Times.Once);
        tickets.Verify(t => t.FinalizeAsync(
            It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never, "a notice keeps the ticket's status — it is not a park and not a close");
    }

    [Fact]
    public async Task Notice_InlineTicketOrNoTracker_PostsNothing()
    {
        var tickets = new Mock<ITicketProvider>();
        var unanswered = new UnansweredQuestion(Previous(taken: 0).Handback!, "(a)", ReadingA);
        var inline = new PipelineContext();
        inline.Set(ContextKeys.Ticket, Ticket());
        inline.Set(ContextKeys.InlineTicket, new Contracts.Models.InlineTicket("t", "d"));

        await Notice(tickets).PostAsync(inline, Tracker(), unanswered, default);
        await Notice(tickets).PostAsync(new PipelineContext(), null, unanswered, default);

        tickets.Verify(t => t.UpdateStatusAsync(
            It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static UnansweredQuestionNotice Notice(Mock<ITicketProvider> tickets)
    {
        var factory = new Mock<ITicketProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(tickets.Object);
        return new UnansweredQuestionNotice(factory.Object, NullLogger<UnansweredQuestionNotice>.Instance);
    }

    private static SpecSet Previous(int taken) => new(
        "azdo-1", [], SpecAccounting.Empty,
        [new SpecRevision(1, SpecRevisionCause.Initial, Asked.AddMinutes(-1))],
        SpecSource.BranchArtifact,
        new SpecHandback(SpecHandbackCase.Question, "reads two ways", Readings: [ReadingA, ReadingB], Taken: taken));

    private static TicketComment OurQuestion(DateTimeOffset at) => Comment(
        "agent-smith", at,
        SpecHandbackComment.Build(Previous(taken: 0).Handback!, null, string.Empty));

    private static TicketComment Comment(string author, DateTimeOffset at, string body) => new(author, at, body);

    private static PipelineContext PipelineWith(params TicketComment[] comments)
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyList<TicketComment>>(ContextKeys.TicketComments, comments);
        return pipeline;
    }

    private static Ticket Ticket() =>
        new(new TicketId("1"), "Adopt the newest versions", "Adopt the newest versions, even breaking.", null, "open", "azdo", []);

    private static TrackerConnection Tracker() => new() { Type = TrackerType.AzureDevOps };
}
