using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Services.Lifecycle;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// p0461: an answer written on the work item reaches the parked run, and the board learns
/// the run moved on.
/// <para>
/// The dashboard RESUMES the same run; the ticket, until now, could only RESTART it via a
/// hand-moved status that threw the parked run away — and the person typing into the ticket
/// had no way to know which of the two they were doing.
/// </para>
/// </summary>
public sealed class AnAnswerOnTheTicketReachesTheRunTests
{
    private static readonly DateTimeOffset Asked = DateTimeOffset.Parse("2026-08-19T10:00:00Z");

    [Fact]
    public async Task AnOperatorReplyOnTheTicket_ReachesTheParkedRun()
    {
        var inbox = new RecordingInbox();
        var sut = Build(inbox, Reply("jane", Asked.AddMinutes(55), "Q1: option-a"));

        (await sut.TryCollectAnswerAsync(Checkpoint(), CancellationToken.None)).Should().BeTrue();

        inbox.Delivered.Should().ContainSingle()
            .Which.Answer.Should().Be("Q1: option-a",
                "the reply the operator wrote on the ticket IS the answer to the question");
        inbox.Delivered[0].AnsweredBy.Should().Be("jane");
    }

    [Fact]
    public async Task OurOwnQuestionComment_IsNeverReadBackAsItsAnswer()
    {
        var inbox = new RecordingInbox();
        var sut = Build(inbox, Reply(
            "Agent Smith", Asked.AddSeconds(1),
            "<!--agent-smith:open-questions--> **Agent Smith — open questions**"));

        (await sut.TryCollectAnswerAsync(Checkpoint(), CancellationToken.None)).Should().BeFalse();

        inbox.Delivered.Should().BeEmpty("answering the question with itself is how a loop starts");
    }

    [Fact]
    public async Task ACommentOlderThanTheQuestion_IsNotItsAnswer()
    {
        var inbox = new RecordingInbox();
        var sut = Build(inbox, Reply("jane", Asked.AddMinutes(-5), "please use the shared package"));

        (await sut.TryCollectAnswerAsync(Checkpoint(), CancellationToken.None)).Should().BeFalse();

        inbox.Delivered.Should().BeEmpty(
            "a comment written before the question cannot be an answer to it");
    }

    [Fact]
    public async Task TheSameReply_IsDeliveredOnlyOnce()
    {
        var inbox = new RecordingInbox();
        var sut = Build(inbox, Reply("jane", Asked.AddMinutes(1), "option-a"));

        await sut.TryCollectAnswerAsync(Checkpoint(), CancellationToken.None);
        var second = await sut.TryCollectAnswerAsync(Checkpoint(), CancellationToken.None);

        second.Should().BeFalse("the inbox is first-answer-wins — the poll may repeat, the answer may not");
        inbox.Delivered.Should().ContainSingle();
    }

    /// <summary>
    /// 2026-08-25-a508: the run asked again, so the ticket's next reply answers the SECOND
    /// question. Under one identity per run there was one answerable slot for ever: the reply
    /// lost to the first question's row and the run waited out its patience on a question
    /// nobody could answer.
    /// </summary>
    [Fact]
    public async Task TicketComment_AfterASecondAsk_IsDeliveredToTheSecondQuestion()
    {
        var inbox = new RecordingInbox();
        var askedAgain = Asked.AddHours(2);
        var sut = Build(inbox, [
            Reply("jane", Asked.AddMinutes(5), "option-a"),
            Reply("jane", askedAgain.AddMinutes(5), "option-b"),
        ]);

        (await sut.TryCollectAnswerAsync(Checkpoint(), CancellationToken.None)).Should().BeTrue();
        var secondAsk = Checkpoint() with { QuestionId = "ask-2", AskedAt = askedAgain };
        (await sut.TryCollectAnswerAsync(secondAsk, CancellationToken.None)).Should().BeTrue();

        inbox.Delivered.Should().HaveCount(2);
        inbox.Delivered[1].QuestionId.Should().Be("ask-2");
        inbox.Delivered[1].Answer.Should().Be("option-b",
            "the reply written after the second ask answers the second ask");
    }

    [Fact]
    public async Task ATrackerWithNoPoll_IsLeftToItsWebhooks()
    {
        var inbox = new RecordingInbox();
        var sut = Build(inbox, Reply("jane", Asked.AddMinutes(1), "option-a"), TrackerType.GitHub);

        (await sut.TryCollectAnswerAsync(Checkpoint(), CancellationToken.None)).Should().BeFalse();

        inbox.Delivered.Should().BeEmpty("only Azure DevOps is polled in this phase");
    }

    private static ParkedTicketDialogue Build(
        RecordingInbox inbox, TicketComment comment, TrackerType type = TrackerType.AzureDevOps) =>
        Build(inbox, [comment], type);

    private static ParkedTicketDialogue Build(
        RecordingInbox inbox, IReadOnlyList<TicketComment> comments,
        TrackerType type = TrackerType.AzureDevOps)
    {
        var (factory, _) = ParkedTicketFixture.Provider(comments);
        return new ParkedTicketDialogue(
            ParkedTicketFixture.Loader(type), ParkedTicketFixture.Context, factory, inbox,
            NullLogger<ParkedTicketDialogue>.Instance);
    }

    private static TicketComment Reply(string author, DateTimeOffset at, string body) =>
        new(author, at, body);

    private static RunCheckpointRecord Checkpoint() => ParkedTicketFixture.Checkpoint(Asked);

    private sealed class RecordingInbox : IDialogueAnswerInbox
    {
        public List<DialogAnswer> Delivered { get; } = [];

        public Task<bool> TryDeliverAsync(string dialogueJobId, DialogAnswer answer, CancellationToken ct)
        {
            // The production inbox drops a duplicate (dialogueJobId, questionId) on its unique
            // index. This says the same thing without a database.
            if (Delivered.Any(a => a.QuestionId == answer.QuestionId)) return Task.FromResult(false);
            Delivered.Add(answer);
            return Task.FromResult(true);
        }

        public Task<DialogAnswer?> GetAsync(string dialogueJobId, string questionId, CancellationToken ct) =>
            Task.FromResult(Delivered.FirstOrDefault(a => a.QuestionId == questionId));
    }
}
