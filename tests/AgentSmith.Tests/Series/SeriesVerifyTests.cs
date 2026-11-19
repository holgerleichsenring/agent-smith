using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Series;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Series;

/// <summary>2026-11-17-0207d: the series check starts once every ticket landed, records each criterion (an unrecorded
/// one counts as not shown), files only failures not followed up before, reports once, and ends the series when it
/// found nothing new.</summary>
public sealed class SeriesVerifyTests
{
    private readonly InMemorySeriesRunStore _store = new();
    private readonly Mock<ISeriesRunLaunch> _launch = new();

    private static readonly SeriesCriterion[] Criteria =
        [new("SCRUM-43", "s-1", "requests persist"), new("SCRUM-44", "s-2", "expired requests are notified")];

    private SeriesProgress Progress => new(_store, _launch.Object);

    private async Task<SeriesBeat> BeatAsync(SeriesRunRecord record, bool allLanded = true)
    {
        await _store.OpenAsync(record, default);
        var facts = record.Tickets.Select(t => new SeriesTicketFact(t) { LandedAt = allLanded ? SeriesRecords.T : null, CostUsd = 10m }).ToList();
        return new SeriesBeat(record, facts, SeriesRecords.T.AddHours(4));
    }

    [Fact]
    public void VerdictTool_UnrecordedCriterion_CountsAsNotShown()
    {
        var tool = new SeriesVerdictTool(Criteria);
        tool.RecordCriterion(1, true, "dotnet test: 12 passed");

        tool.Results().Select(r => r.Passed).Should().Equal(true, false);
        tool.Results()[1].Evidence.Should().StartWith("not checked");
        tool.RecordCriterion(3, true, "x").Should().Contain("1 to 2");
    }

    [Fact]
    public void FollowUp_OnlyFailuresNotFollowedUpBefore_OneWorkTicketNamingThem()
    {
        var verdict = new SeriesVerdict("run-v", SeriesRecords.T, [new(Criteria[0].Cited, false, "no row"), new(Criteria[1].Cited, false, "silent")]);
        var record = SeriesRecords.Running() with { FollowedUp = [Criteria[0].Cited] };

        var fresh = SeriesFollowUp.New(record, verdict);
        var request = SeriesFollowUp.Request(record, fresh);

        fresh.Should().ContainSingle().Which.Criterion.Should().Be(Criteria[1].Cited);
        request.FollowUp!.Title.Should().Be("Series check: expired requests are notified");
        request.FollowUp.Body.Should().Contain("Found: silent");
        request.Principal.Should().Be("operator");
    }

    [Fact]
    public async Task Progress_EveryTicketLanded_StartsTheCheck()
    {
        _launch.Setup(l => l.LaunchAsync("sample", PipelinePresets.SeriesVerifyName, "sr-1", It.IsAny<CancellationToken>())).ReturnsAsync("run-v");
        var beat = await BeatAsync(SeriesRecords.Running());

        await Progress.MoveAsync(beat, default);

        _store.Records["sr-1"].Should().Match<SeriesRunRecord>(r => r.State == SeriesRunStates.Verifying && r.VerifyRunId == "run-v");
        beat.Said.Should().ContainSingle().Which.Should().Contain("series check on main runs now");
    }

    [Fact]
    public async Task Progress_VerdictWithAFollowUp_ReportsAndKeepsRunning()
    {
        var verdict = new SeriesVerdict("run-v", SeriesRecords.T, [new(Criteria[0].Cited, true, "ok"), new(Criteria[1].Cited, false, "silent")])
        { FollowUpTicketId = "68" };
        var record = SeriesRecords.Running() with
        {
            Verdicts = [verdict], Tickets = [.. SeriesRecords.Running().Tickets, SeriesRecords.Ticket("68") with { FollowUp = true }],
        };
        var beat = await BeatAsync(record, allLanded: false);

        await Progress.MoveAsync(beat, default);

        beat.Said.Should().ContainSingle().Which.Should().Contain("1 of 2 hold").And.Contain("I filed SCRUM-68 for it in this series; it is running")
            .And.EndWith("Nothing waits on you.");
        _store.Records["sr-1"].Should().Match<SeriesRunRecord>(r => r.State == SeriesRunStates.Running && r.Verdicts[0].Reported);
    }

    [Fact]
    public async Task Progress_VerdictWithNothingNew_EndsTheSeries()
    {
        var verdict = new SeriesVerdict("run-v", SeriesRecords.T, [new(Criteria[0].Cited, true, "ok")]);
        var beat = await BeatAsync(SeriesRecords.Running() with { State = SeriesRunStates.Verifying, VerifyRunId = "run-v", Verdicts = [verdict] });

        await Progress.MoveAsync(beat, default);

        _store.Records["sr-1"].State.Should().Be(SeriesRunStates.Done);
        beat.Said.Single().Should().Contain("1 of 1 hold").And.Contain("Total $30.0, 4 h 00 min").And.EndWith("Nothing waits on you.");
    }

    [Fact]
    public async Task Progress_CheckEndedWithoutVerdict_AsksTheMaster()
    {
        _launch.Setup(l => l.FinishedAsync("run-v", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var beat = await BeatAsync(SeriesRecords.Running() with { State = SeriesRunStates.Verifying, VerifyRunId = "run-v" });

        await Progress.MoveAsync(beat, default);

        beat.Reasons.Should().ContainSingle().Which.Should().Contain("check_series");
    }

    // 2026-11-19-1b72a: a verdict that arrives while the operator paused the check still ends the series.
    [Fact]
    public async Task Progress_VerdictArrivingDuringAPause_EndsTheSeries()
    {
        var verdict = new SeriesVerdict("run-v", SeriesRecords.T, [new(Criteria[0].Cited, true, "ok")]);
        var beat = await BeatAsync(SeriesRecords.Running() with
        {
            State = SeriesRunStates.Paused, PausedFrom = SeriesRunStates.Verifying, VerifyRunId = "run-v", Verdicts = [verdict],
        });

        await Progress.MoveAsync(beat, default);

        _store.Records["sr-1"].Should().Match<SeriesRunRecord>(r => r.State == SeriesRunStates.Done && r.PausedFrom == null);
    }

    [Fact]
    public async Task CheckSeries_WhilePaused_StartsNoCheckAndKeepsThePause()
    {
        var beat = await BeatAsync(SeriesRecords.Running() with { State = SeriesRunStates.Paused, PausedFrom = SeriesRunStates.Running });

        (await Progress.CheckAsync(beat, default)).Should().Contain("paused");

        _store.Records["sr-1"].State.Should().Be(SeriesRunStates.Paused);
        _launch.VerifyNoOtherCalls();
    }

    // A pause pressed while this beat launched the check is kept; the check resumes into Verifying.
    [Fact]
    public async Task CheckSeries_PausePressedMidBeat_KeepsThePause()
    {
        _launch.Setup(l => l.LaunchAsync("sample", PipelinePresets.SeriesVerifyName, "sr-1", It.IsAny<CancellationToken>())).ReturnsAsync("run-v");
        var beat = await BeatAsync(SeriesRecords.Running());
        await _store.UpdateAsync("sr-1", r => r with { State = SeriesRunStates.Paused, PausedFrom = SeriesRunStates.Running }, default);

        await Progress.CheckAsync(beat, default);

        _store.Records["sr-1"].Should().Match<SeriesRunRecord>(r =>
            r.State == SeriesRunStates.Paused && r.PausedFrom == SeriesRunStates.Verifying && r.VerifyRunId == "run-v");
    }
}
