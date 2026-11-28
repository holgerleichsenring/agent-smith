using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Series;
using FluentAssertions;

namespace AgentSmith.Tests.Series;

/// <summary>2026-11-27-3532b: a ticket's question relayed into the chat before its run asked its master is dropped at the next
/// beat, unanswered — even with an operator message in that beat — and the run's question goes to the master instead. No
/// boot migration does it: the next beat does. The master's own question is never dropped.</summary>
public sealed class SeriesQuestionsStaleTests
{
    private readonly InMemorySeriesRunStore _store = new();

    private static readonly DateTimeOffset Later = SeriesRecords.T.AddDays(1);

    private static SeriesTicketFact Parked(string ticket, string text) =>
        SeriesRecords.Fact(ticket, RunStatuses.WaitingForInput, finished: false) with
        {
            DialogueJobId = $"run-{ticket}",
            PendingQuestion = new DialogQuestion($"q-{ticket}", QuestionType.FreeText, text, null, null, null, TimeSpan.FromDays(14)),
        };

    private static SeriesQuestion Relayed(string ticket, string text) =>
        SeriesQuestions.Ask(text, [], null, SeriesRecords.T, 120) with
        {
            TicketId = ticket, DialogueJobId = $"run-{ticket}", CheckpointQuestionId = $"q-{ticket}",
        };

    private async Task<SeriesBeat> BeatAsync(SeriesQuestion question, IReadOnlyList<SeriesTicketFact> facts, params string[] messages)
    {
        await _store.OpenAsync(SeriesRecords.Running() with { Question = question }, default);
        var beat = new SeriesBeat(_store.Records["sr-1"], facts, Later);
        beat.Messages.AddRange(messages.Select((m, i) => new SeriesOperatorMessage(20 + i, m, Later, "Dana")));
        await new SeriesQuestions(_store, AgentSmith.Tests.TestSupport.SeriesWakeDoubles.Budget(_store), new AgentSmith.Contracts.Models.Configuration.DialogueGlobalConfig()).SettleAsync(beat, default);
        return beat;
    }

    [Fact]
    public async Task RelayedQuestion_StillParked_IsDropped_AndItsRunAsksTheMaster()
    {
        var beat = await BeatAsync(Relayed("44", "Use the v2 payload?"), [Parked("44", "Use the v2 payload?")], "do it like that");

        _store.Records["sr-1"].Question.Should().BeNull("the operator's message answers no ticket's run any more");
        beat.Said.Should().BeEmpty();
        beat.Reasons.Should().Contain(r => r.Contains("SCRUM-44's run is parked on a question to you") && r.Contains("Use the v2 payload?"));
        beat.MasterAnswered.Should().BeFalse();
    }

    [Fact]
    public async Task MastersOwnQuestion_IsNeverDropped()
    {
        await BeatAsync(SeriesQuestions.Ask("Ship on Friday?", [], null, SeriesRecords.T, 120),
            [SeriesRecords.Fact("43", RunStatuses.Running, finished: false)]);

        _store.Records["sr-1"].Question!.Text.Should().Be("Ship on Friday?");
    }
}
