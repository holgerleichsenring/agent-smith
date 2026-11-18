using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Series;
using FluentAssertions;

namespace AgentSmith.Tests.Series;

/// <summary>2026-11-17-0207b: the series opens with what it filed, started and waits on and its policy; the table is
/// posted on a change and on the cadence, not otherwise; the master's line and the beat's own words come first.</summary>
public sealed class SeriesReportTests
{
    private static readonly ResolvedProject Project = new()
    {
        Name = "sample", Merge = new MergePolicyConfig { When = MergeWhen.OnGateGreen },
    };

    private static readonly DialogueGlobalConfig Dialogue = new();

    private static SeriesTicketFact[] Facts() =>
        [SeriesRecords.Fact("43", RunStatuses.Running, finished: false), SeriesRecords.Fact("44", requires: "43"),
         SeriesRecords.Fact("45", requires: "43")];

    [Fact]
    public void FirstBeat_OpensWithWhatStartedWhatWaitsAndThePolicy()
    {
        var messages = SeriesReport.Messages(new SeriesBeat(SeriesRecords.Running(), Facts(), SeriesRecords.T), Project, Dialogue);

        messages[0].Should().Be("Filed 3 tickets. SCRUM-43 started; 2 wait on their prerequisites and start as soon as the "
            + "ticket they need has a green push. I merge each one when its gate is green and report here on every change and every 15 minutes.");
        messages[1].Should().Contain("| SCRUM-43 | work | coding |").And.Contain("1 running · 2 held");
    }

    [Fact]
    public void LaterBeat_NothingChangedBeforeTheCadence_PostsNoTable()
    {
        var beat = new SeriesBeat(SeriesRecords.Running(), Facts(), SeriesRecords.T.AddMinutes(10));
        beat.Record = beat.Record with { LastStatusAt = SeriesRecords.T, LastTable = beat.Table };
        beat.MasterLine = "Passed to SCRUM-44.";

        SeriesReport.Messages(beat, Project, Dialogue).Should().Equal("Passed to SCRUM-44.");
    }

    [Fact]
    public void LaterBeat_CadenceDue_PostsTheTableAgain()
    {
        var beat = new SeriesBeat(SeriesRecords.Running(), Facts(), SeriesRecords.T.AddMinutes(15));
        beat.Record = beat.Record with { LastStatusAt = SeriesRecords.T, LastTable = beat.Table };

        SeriesReport.Messages(beat, Project, Dialogue).Should().ContainSingle().Which.Should().StartWith("Status at 18:52 UTC.");
    }

    [Fact]
    public void Stalled_TableStillForAPeriodWhileTicketsRun_AsksTheMaster()
    {
        var beat = new SeriesBeat(SeriesRecords.Running(), Facts(), SeriesRecords.T.AddMinutes(15));
        beat.Record = beat.Record with { LastStatusAt = SeriesRecords.T, LastTable = beat.Table };

        SeriesJudgement.Stalled(beat, 15).Should().ContainSingle().Which.Should().Contain("SCRUM-43");
    }

    [Fact]
    public void Policy_Line_NamesMergeStartCadenceAndQuestionDefault() =>
        SeriesPolicy.Line(Project, Dialogue).Should().Be(
            "merge on green gate · start on green push · status every 15 min · questions default to the recommendation after 2 h 00 min");
}
