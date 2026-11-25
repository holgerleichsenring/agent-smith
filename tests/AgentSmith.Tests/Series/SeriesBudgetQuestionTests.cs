using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Series;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Series;

/// <summary>2026-11-25-9992g: once no more than dialogue.series_master_budget_ask_usd is left, the series asks once — raise
/// or stop, "raise" recommended and no deadline; "raise" lets the master judge again, "stop" stops the series, and any
/// other answer goes to the master.</summary>
public sealed class SeriesBudgetQuestionTests
{
    private static readonly DateTimeOffset Now = SeriesRecords.T.AddHours(1);
    private readonly SeriesWakeBeatRig _rig = new();

    public SeriesBudgetQuestionTests()
    {
        _rig.Config.Dialogue.SeriesMasterBudgetUsd = 20m;
        _rig.Config.Dialogue.SeriesMasterBudgetAskUsd = 2m;
    }

    private SeriesRunRecord Spent(decimal cost) => SeriesRecords.Running() with { MasterCostUsd = cost };

    [Fact]
    public async Task BelowTheAskThreshold_AsksOnce_WithChoicesAndNoDeadline()
    {
        var first = await _rig.BeatAsync(Spent(18.5m), Now, new SeriesOperatorMessage(1, "status?", Now));
        _rig.Store.Records["sr-1"] = _rig.Store.Records["sr-1"] with { Question = null }; // the slot is free again
        var second = await _rig.BeatAsync(_rig.Store.Records["sr-1"], Now.AddMinutes(5), new SeriesOperatorMessage(2, "and now?", Now));

        var asked = first.Said.Should().ContainSingle(s => s.Contains("dialogue.series_master_budget_ask_usd")).Subject;
        asked.Should().Contain("dialogue.series_master_budget_usd").And.Contain("Raise the budget by $20.0");
        second.Said.Should().NotContain(s => s.Contains("dialogue.series_master_budget_ask_usd"));
        _rig.Prompts.Should().HaveCount(2, "the master still judges while budget is left");
    }

    [Fact]
    public async Task TheBudgetQuestion_IsNeverTakenByDefault()
    {
        await AskedAsync(cost: 19m);

        await _rig.BeatAsync(_rig.Store.Records["sr-1"], Now.AddDays(3));

        var question = _rig.Store.Records["sr-1"].Question!;
        question.Answer.Should().BeNull("a budget is never raised, nor a series stopped, by default");
        question.Recommended.Should().StartWith("Raise", "the page draws its choices as buttons");
    }

    [Fact]
    public async Task ARaiseAnswer_RaisesTheBudget_AndTheMasterJudgesAgain()
    {
        await AskedAsync(cost: 20m);

        var beat = await _rig.BeatAsync(_rig.Store.Records["sr-1"], Now, new SeriesOperatorMessage(3, "Raise the budget by $20.0", Now));

        var record = _rig.Store.Records["sr-1"];
        (record.BudgetRaisedUsd, record.BudgetUsd(20m), record.BudgetUsd(30m)).Should().Be((20m, 40m, 50m),
            "a raise is kept as what was added, so a later change of the setting still applies");
        (record.BudgetAsked, record.BudgetSaid, record.Question!.Delivered).Should().Be((false, false, true));
        beat.Said.Should().Contain(s => s.Contains("raised to $40.0"));
        _rig.Prompts.Should().ContainSingle("the raised budget lets it judge the operator's answer");
    }

    [Fact]
    public async Task AStopAnswer_StopsTheSeries()
    {
        await AskedAsync(cost: 20m);

        await _rig.BeatAsync(_rig.Store.Records["sr-1"], Now, new SeriesOperatorMessage(3, "stop", Now));

        _rig.Store.Records["sr-1"].State.Should().Be(SeriesRunStates.Stopped);
        _rig.Loop.Verify(l => l.RunAsync(It.IsAny<AgentSmith.Application.Services.Loop.AgenticLoopRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AnyOtherAnswer_GoesToTheMaster()
    {
        await AskedAsync(cost: 19m);

        var beat = await _rig.BeatAsync(_rig.Store.Records["sr-1"], Now, new SeriesOperatorMessage(3, "why so expensive?", Now));

        _rig.Store.Records["sr-1"].BudgetRaisedUsd.Should().Be(0m);
        beat.Reasons.Should().Contain(r => r.Contains("is answered") && r.Contains("why so expensive?"));
        beat.Said.Should().NotContain(s => s.Contains("dialogue.series_master_budget_ask_usd"), "not asked again in the beat it was answered");
        _rig.Store.Records["sr-1"].BudgetAsked.Should().BeFalse();

        var next = await _rig.BeatAsync(_rig.Store.Records["sr-1"], Now.AddMinutes(1));

        next.Said.Should().ContainSingle(s => s.Contains("dialogue.series_master_budget_ask_usd"), "asked again in a later beat");
    }

    [Theory]
    [InlineData("Stop restarting CI on SCRUM-12", null)]
    [InlineData("raise it a bit but not much", null)]
    [InlineData("1", null)]
    [InlineData("  Stop. ", 1)]
    [InlineData("RAISE!", 0)]
    [InlineData("Raise the budget by $20.0", 0)]
    [InlineData("stop the series", 1)]
    public void AnAnswer_IsAChoiceOnlyWhenItIsOne(string answer, int? choice)
    {
        var question = new SeriesQuestion("q", "?", ["Raise the budget by $20.0", "Stop the series"], 0, Now, null) { Answer = answer };

        SeriesBudgetAnswer.Choice(question).Should().Be(choice);
    }

    [Fact]
    public async Task TheBeatThatSpendsItDown_AsksEvenWithoutAnythingToJudgeNext()
    {
        await _rig.Store.OpenAsync(Spent(19.5m) with { Question = null }, default);

        var quiet = await _rig.BeatAsync(_rig.Store.Records["sr-1"], Now); // no reasons at all

        quiet.Said.Should().ContainSingle(s => s.Contains("dialogue.series_master_budget_ask_usd"));
        _rig.Prompts.Should().BeEmpty();
    }

    [Fact]
    public async Task TheBudgetQuestion_GoesBeforeARelayedTicketQuestion_ButAfterTheMastersOwn()
    {
        var relayed = SeriesQuestions.Ask("Which name?", ["a", "b"], null, Now, 120) with { TicketId = "43", CheckpointQuestionId = "c1" };
        await _rig.Store.OpenAsync(Spent(19m) with { Question = relayed }, default);
        var beat = new SeriesBeat(_rig.Store.Records["sr-1"], [], Now);

        await new SeriesBudgetQuestion(_rig.Store, _rig.Config).AskIfLowAsync(beat, default);

        _rig.Store.Records["sr-1"].Question!.Budget.Should().BeTrue("the ticket's run still parks and is relayed again once this is answered");

        var own = SeriesQuestions.Ask("Merge now?", ["yes", "no"], null, Now, 120);
        _rig.Store.Records["sr-1"] = Spent(19m) with { Question = own };
        await new SeriesBudgetQuestion(_rig.Store, _rig.Config).AskIfLowAsync(new SeriesBeat(_rig.Store.Records["sr-1"], [], Now), default);

        _rig.Store.Records["sr-1"].Question.Should().Be(own);
    }

    [Fact]
    public async Task TheSpentLine_NamesWhatLetsItJudgeAgain()
    {
        var budget = new SeriesBudgetQuestion(_rig.Store, _rig.Config);
        await _rig.Store.OpenAsync(Spent(20m) with { Question = SeriesQuestions.Ask("Merge now?", ["yes"], null, Now, 120) }, default);
        var blocked = new SeriesBeat(_rig.Store.Records["sr-1"], [], Now);

        await budget.SpentAsync(blocked, default);

        blocked.Said.Should().ContainSingle().Which.Should().Contain("Raise dialogue.series_master_budget_usd");
    }

    // The question as the series asked it, waiting for the operator.
    private async Task AskedAsync(decimal cost)
    {
        await _rig.Store.OpenAsync(Spent(cost), default);
        await new SeriesBudgetQuestion(_rig.Store, _rig.Config).AskIfLowAsync(new SeriesBeat(Spent(cost), [], Now), default);
        _rig.Store.Records["sr-1"].Question.Should().Match<SeriesQuestion>(q => q.Budget && q.NoDefault && q.DeadlineAt == null
            && q.RecommendedIndex == 0 && q.Choices.Count == 2);
    }
}
