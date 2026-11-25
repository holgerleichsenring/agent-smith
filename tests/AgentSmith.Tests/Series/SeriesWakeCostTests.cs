using AgentSmith.Application.Services.Loop;
using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Series;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Series;

/// <summary>2026-11-25-96aec: a master turn costs money, so one starts only when a decision is due — an idle series makes no
/// model call however long it sweeps, a burst of events is one turn, an event of the master's own act or one recorded quietly
/// starts none, and a check that ended without a verdict is told once.</summary>
public sealed class SeriesWakeCostTests
{
    private static readonly DateTimeOffset Now = SeriesRecords.T.AddHours(1);
    private readonly SeriesWakeBeatRig _rig = new();

    private static SeriesWake Wake(string kind, string text, int secondsAgo, bool quiet = false, string ticket = "43") =>
        new SeriesWake(kind, text) { TicketId = ticket, At = Now.AddSeconds(-secondsAgo), Quiet = quiet };

    private SeriesRunRecord Seen(params SeriesWake[] wakes) =>
        _rig.Seen(SeriesRecords.Running() with { Wakes = wakes }, Now);

    private void NoModelCall() =>
        _rig.Loop.Verify(l => l.RunAsync(It.IsAny<AgenticLoopRequest>(), It.IsAny<CancellationToken>()), Times.Never());

    // Sweeps the series like SeriesRunSweeper does, beating it whenever it is due; answers the beats run.
    private async Task<int> SweepAsync(SeriesRunRecord record, int sweeps, TimeSpan every)
    {
        await _rig.Store.OpenAsync(record, default);
        var beats = 0;
        for (var i = 0; i < sweeps; i++)
        {
            var now = Now.Add(every * i);
            if (await _rig.DueAsync(_rig.Store.Records[record.Id], now) is null) continue;
            await _rig.BeatAsync(_rig.Store.Records[record.Id], now);
            beats++;
        }
        return beats;
    }

    [Fact]
    public async Task AnIdleRunningSeries_MakesNoModelCall_OverTwoDaysOfSweeps()
    {
        _rig.Facts = [SeriesRecords.Fact("43", RunStatuses.Running, finished: false), SeriesRecords.Fact("44", RunStatuses.Running, finished: false)];

        var beats = await SweepAsync(Seen(), sweeps: 200, every: TimeSpan.FromMinutes(15));

        beats.Should().Be(0, "nothing changed, so nothing is started");
        NoModelCall();
    }

    [Fact]
    public async Task QuietEventsAlone_StartNoTurn_AndRideAlongWithTheNextReal()
    {
        var quiet = Wake(SeriesWakeKinds.PullRequest, "pull request https://host/pr/1 no longer conflicts with its base", 600, quiet: true);

        (await SweepAsync(Seen(quiet), sweeps: 50, every: TimeSpan.FromMinutes(15))).Should().Be(0);
        NoModelCall();

        var red = Wake(SeriesWakeKinds.Ci, "the CI of pull request https://host/pr/1 on abc1234 is red", 120);
        var record = _rig.Store.Records["sr-1"] with { Wakes = [quiet, red] };
        _rig.Store.Records["sr-1"] = record;
        (await _rig.DueAsync(record, Now)).Should().Be(SeriesBeatDue.EventsArrived);
        await _rig.BeatAsync(record, Now);

        _rig.Prompts.Should().ContainSingle().Which.Should().Contain("is red").And.Contain("Recorded meanwhile, for your picture")
            .And.Contain("no longer conflicts");
        _rig.Store.Records["sr-1"].Wakes.Should().BeEmpty();
    }

    [Fact]
    public async Task ABurstInsideTheSettleWindow_IsOneTurn()
    {
        var record = Seen(Wake(SeriesWakeKinds.Ci, "the CI of 43 is green", 50), Wake(SeriesWakeKinds.Ci, "the CI of 44 is green", 30, ticket: "44"),
            Wake(SeriesWakeKinds.Run, "its run run-45 ended success", 10, ticket: "45"));

        (await _rig.DueAsync(record, Now)).Should().BeNull("the oldest is 50 s old, below dialogue.series_wake_settle_seconds = 60");
        (await _rig.DueAsync(record, Now.AddSeconds(10))).Should().Be(SeriesBeatDue.EventsArrived);
        await _rig.BeatAsync(record, Now.AddSeconds(10));

        _rig.Prompts.Should().ContainSingle().Which.Should().Contain("the CI of 43").And.Contain("the CI of 44").And.Contain("run-45");
    }

    [Fact]
    public async Task AnOperatorMessage_TakesTheUnsettledEventsAtOnce()
    {
        var record = Seen(Wake(SeriesWakeKinds.Ci, "the CI of 43 is red", 5));

        await _rig.BeatAsync(record, Now, new SeriesOperatorMessage(3, "how is it going?", Now, "operator"));

        _rig.Prompts.Should().ContainSingle().Which.Should().Contain("how is it going?").And.Contain("the CI of 43 is red");
    }

    [Fact]
    public async Task AnEventOfTheMastersOwnAct_StartsNoTurn_AndOneAfterItDoes()
    {
        var act = new SeriesAct(Now.AddMinutes(-4), SeriesPullRequestTools.MergeTool, "43", "merged SCRUM-43 (now)", "green")
        { StartedAt = Now.AddMinutes(-3), DoneAt = Now.AddMinutes(-2) };
        var own = Wake(SeriesWakeKinds.PullRequest, "pull request https://host/pr/1 is merged", 150);
        var record = Seen(own) with { Acts = [act] };

        (await _rig.DueAsync(record, Now)).Should().BeNull("its merge recorded it, and the act's answer told the master");
        await _rig.BeatAsync(record, Now);
        NoModelCall();

        var later = Wake(SeriesWakeKinds.PullRequest, "pull request https://host/pr/2 is merged", 90);
        var earlier = Wake(SeriesWakeKinds.Ci, "the CI of pull request https://host/pr/1 on abc1234 is red", 210);
        (await _rig.DueAsync(record with { Wakes = [own, later] }, Now)).Should().Be(SeriesBeatDue.EventsArrived,
            "a merge read after the act returned is somebody else's");
        SeriesBeatDue.Waking(record, earlier).Should().BeTrue("recorded in the beat before the act was called");
    }

    [Fact]
    public async Task AFailedMaster_IsBeatenForNothing_UntilItsWaitPassed()
    {
        var failed = SeriesRecords.Running() with { MasterFailedAt = Now, LastTable = "SCRUM-43=red · failed" };

        (await _rig.DueAsync(failed, Now.AddMinutes(1))).Should().BeNull("its reasons stay for the beat after the wait");
        (await _rig.DueAsync(failed, Now.AddMinutes(15))).Should().Be("the table changed");
    }

    [Fact]
    public async Task ACheckEndedWithoutAVerdict_IsToldOnce()
    {
        _rig.Facts = [SeriesRecords.Fact("43", landed: SeriesRecords.T)];
        _rig.Launch.Setup(l => l.FinishedAsync("v-1", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var record = Seen() with { State = SeriesRunStates.Verifying, VerifyRunId = "v-1" };

        (await _rig.DueAsync(record, Now)).Should().Be(SeriesBeatDemand.CheckEnded);
        await _rig.BeatAsync(record, Now);

        _rig.Prompts.Should().ContainSingle().Which.Should().Contain("ended without a verdict");
        _rig.Store.Records["sr-1"].Raised.Should().Contain(SeriesBeatDemand.CheckKey("v-1"));
        (await _rig.DueAsync(_rig.Store.Records["sr-1"], Now.AddMinutes(30))).Should().BeNull();
        await _rig.BeatAsync(_rig.Store.Records["sr-1"], Now.AddMinutes(30));
        _rig.Prompts.Should().ContainSingle("it is told once per check run");
    }
}
