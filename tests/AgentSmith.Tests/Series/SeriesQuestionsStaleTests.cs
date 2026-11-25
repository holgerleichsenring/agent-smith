using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Series;
using AgentSmith.Contracts.Services;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Series;

/// <summary>2026-11-25-e94ea: a relayed question whose ticket's run left it is dropped at the start of the beat and the
/// next parked question is relayed, so the operator's answer reaches the run that asks — the live sequence in which a
/// stale question of one ticket took the answer meant for another.</summary>
public sealed class SeriesQuestionsStaleTests
{
    private readonly InMemorySeriesRunStore _store = new();
    private readonly Mock<IDialogueAnswerInbox> _inbox = new();

    private static readonly DateTimeOffset Later = SeriesRecords.T.AddDays(1);

    private static SeriesTicketFact Parked(string ticket, string text) =>
        SeriesRecords.Fact(ticket, RunStatuses.WaitingForInput, finished: false) with
        {
            DialogueJobId = $"job-{ticket}",
            PendingQuestion = new DialogQuestion($"q-{ticket}", QuestionType.FreeText, text, null, null, null, TimeSpan.FromDays(14)),
        };

    private static SeriesQuestion Relayed(string ticket, string text) =>
        SeriesQuestions.Ask(text, [], null, SeriesRecords.T, 120) with
        {
            TicketId = ticket, DialogueJobId = $"job-{ticket}", CheckpointQuestionId = $"q-{ticket}",
        };

    private async Task<SeriesBeat> BeatAsync(IReadOnlyList<SeriesTicketFact> facts, DateTimeOffset now, params string[] messages)
    {
        var record = _store.Records["sr-1"];
        var beat = new SeriesBeat(record, facts, now);
        beat.Messages.AddRange(messages.Select((m, i) => new SeriesOperatorMessage(20 + i, m, now, "Dana")));
        await new SeriesQuestions(_store, _inbox.Object, AgentSmith.Tests.TestSupport.SeriesWakeDoubles.Budget(_store)).SettleAsync(beat, 120, default);
        return beat;
    }

    private Task OpenAsync(SeriesQuestion? question) =>
        _store.OpenAsync(SeriesRecords.Running() with { Question = question }, default);

    [Fact]
    public async Task StaleQuestionOfA_BParked_OperatorAnswer_ReachesB_NamingBAndItsQuestion()
    {
        await OpenAsync(Relayed("43", "Old question of 43?"));
        var a = SeriesRecords.Fact("43", RunStatuses.Running, finished: false);
        var b = Parked("44", "Use the v2 payload?\nThe worker reads either.");

        var first = await BeatAsync([a, b], Later);
        first.Said.Should().ContainSingle().Which.Should().Contain("SCRUM-44 asks").And.Contain("Use the v2 payload?");
        _store.Records["sr-1"].Question!.TicketId.Should().Be("44");

        var second = await BeatAsync([a, b], Later.AddMinutes(12), "do it like that");

        _inbox.Verify(i => i.TryDeliverAsync("job-44", It.Is<DialogAnswer>(x => x.QuestionId == "q-44" && x.Answer == "do it like that"),
            It.IsAny<CancellationToken>()), Times.Once);
        _inbox.Verify(i => i.TryDeliverAsync("job-43", It.IsAny<DialogAnswer>(), It.IsAny<CancellationToken>()), Times.Never);
        second.Said.Should().ContainSingle()
            .Which.Should().Be("Passed your answer to SCRUM-44's run on \"Use the v2 payload?\"; it goes on with it.");
    }

    [Fact]
    public async Task StaleQuestionDropped_ThisBeatsMessage_IsNotTakenForTheNewlyRelayedQuestion()
    {
        await OpenAsync(Relayed("43", "Old question of 43?"));

        await BeatAsync([SeriesRecords.Fact("43", RunStatuses.Success), Parked("44", "Use the v2 payload?")], Later, "do it like that");

        _inbox.Verify(i => i.TryDeliverAsync(It.IsAny<string>(), It.IsAny<DialogAnswer>(), It.IsAny<CancellationToken>()), Times.Never);
        _store.Records["sr-1"].Question!.Should().Match<SeriesQuestion>(q => q.TicketId == "44" && q.Answer == null);
    }

    [Fact]
    public async Task StaleQuestion_NothingElseParked_IsDroppedSilently()
    {
        await OpenAsync(Relayed("43", "Old question of 43?"));

        var beat = await BeatAsync([SeriesRecords.Fact("43", RunStatuses.Running, finished: false)], Later);

        _store.Records["sr-1"].Question.Should().BeNull();
        beat.Said.Should().BeEmpty();
    }

    [Fact]
    public async Task StillParkedQuestion_IsAnsweredAsBefore()
    {
        await OpenAsync(Relayed("43", "Keep it?"));

        await BeatAsync([Parked("43", "Keep it?")], Later, "keep it");

        _inbox.Verify(i => i.TryDeliverAsync("job-43", It.Is<DialogAnswer>(x => x.Answer == "keep it"), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task BareFact_AndTheMastersOwnQuestion_AreNeverDropped()
    {
        await OpenAsync(Relayed("43", "Keep it?"));
        await BeatAsync([SeriesRecords.Fact("43")], Later);
        _store.Records["sr-1"].Question!.TicketId.Should().Be("43", "an attempt that was not read is no evidence the run left it");

        _store.Records.Clear();
        await OpenAsync(SeriesQuestions.Ask("Ship on Friday?", [], null, SeriesRecords.T, 120));
        await BeatAsync([SeriesRecords.Fact("43", RunStatuses.Running, finished: false)], Later);
        _store.Records["sr-1"].Question!.Text.Should().Be("Ship on Friday?");
    }
}
