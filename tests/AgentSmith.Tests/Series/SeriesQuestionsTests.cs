using AgentSmith.Application.Services.Series;
using AgentSmith.Application.Services.Triage;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Series;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Series;

/// <summary>2026-11-17-0207c: a series question with a recommendation is taken at its deadline and the conversation
/// says so; one without waits; an operator message answers first; a parked ticket's question is relayed and its answer
/// reaches the parked run's inbox.</summary>
public sealed class SeriesQuestionsTests
{
    private readonly InMemorySeriesRunStore _store = new();
    private readonly Mock<IDialogueAnswerInbox> _inbox = new();

    private SeriesQuestions Questions => new(_store, _inbox.Object);

    private async Task<SeriesBeat> BeatAsync(SeriesQuestion? question, DateTimeOffset now, IReadOnlyList<SeriesTicketFact>? facts = null,
        params string[] messages)
    {
        var record = SeriesRecords.Running() with { Question = question };
        await _store.OpenAsync(record, default);
        var beat = new SeriesBeat(record, facts ?? [], now);
        beat.Messages.AddRange(messages.Select((m, i) => new SeriesOperatorMessage(10 + i, m, now)));
        await Questions.SettleAsync(beat, 120, default);
        return beat;
    }

    private static SeriesQuestion Asked(int? recommended) =>
        SeriesQuestions.Ask("Keep the original deadline?", ["keep", "restart"], recommended, SeriesRecords.T, 120);

    [Fact]
    public async Task RecommendedQuestion_PastItsDeadline_IsTakenByDefaultAndSaysWhen()
    {
        var beat = await BeatAsync(Asked(0), SeriesRecords.T.AddMinutes(121));

        var question = _store.Records["sr-1"].Question!;
        question.Answer.Should().Be("keep");
        question.ByDefault.Should().BeTrue();
        beat.Said.Should().ContainSingle().Which.Should().StartWith("Taken by default at 20:38 UTC: keep");
        beat.Reasons.Should().ContainSingle().Which.Should().Contain("answered by default: keep");
    }

    [Fact]
    public async Task QuestionWithoutRecommendation_WaitsPastAnyTime()
    {
        Asked(null).DeadlineAt.Should().BeNull();

        await BeatAsync(Asked(null), SeriesRecords.T.AddDays(3));

        _store.Records["sr-1"].Question!.Answer.Should().BeNull();
    }

    [Fact]
    public async Task OperatorMessage_AnswersBeforeTheDeadline()
    {
        await BeatAsync(Asked(0), SeriesRecords.T.AddMinutes(5), null, "restart it");

        _store.Records["sr-1"].Question!.Should().Match<SeriesQuestion>(q => q.Answer == "restart it" && !q.ByDefault);
    }

    [Fact]
    public async Task ParkedTicketQuestion_IsRelayed_AndItsAnswerReachesTheRunsInbox()
    {
        var parked = SeriesRecords.Fact("52", "waiting_for_input") with
        {
            DialogueJobId = "job-52",
            PendingQuestion = new DialogQuestion("q-52", QuestionType.Choice, "Forward keeps the deadline?", null,
                [new DialogChoice("keep", "keep"), new DialogChoice("restart", "restart")], null, TimeSpan.FromDays(14), RecommendedIndex: 0),
        };
        var relay = await BeatAsync(null, SeriesRecords.T, [parked]);
        relay.Said.Should().ContainSingle().Which.Should().Contain("SCRUM-52 asks").And.Contain("Recommended: keep");
        var relayed = _store.Records["sr-1"].Question!;
        _store.Records.Clear();

        await BeatAsync(relayed, SeriesRecords.T.AddMinutes(121), [parked]);

        _inbox.Verify(i => i.TryDeliverAsync("job-52", It.Is<DialogAnswer>(a => a.QuestionId == "q-52" && a.Answer == "keep"),
            It.IsAny<CancellationToken>()));
    }

    [Fact]
    public void TicketMasterQuestion_RecommendedIndex_ReachesTheCheckpointQuestion()
    {
        var composed = MasterQuestionCheckpoint.Compose(
            [new PlanOpenQuestion("1", "Keep it?", ["keep", "drop"]) { RecommendedIndex = 1 }], "ask-1");

        composed.RecommendedIndex.Should().Be(1);
        composed.DefaultAnswer.Should().BeNull("outside a series nothing changes: it still waits for a person");
    }
}
