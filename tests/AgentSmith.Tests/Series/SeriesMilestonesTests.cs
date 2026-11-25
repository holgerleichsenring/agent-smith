using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Series;
using FluentAssertions;

namespace AgentSmith.Tests.Series;

/// <summary>2026-11-25-433fa: a merge and a stuck ticket are each told in one sentence, only when they happen — never on the
/// first beat, never for a merge the master made itself the beat before, never beside the master's own act or question.</summary>
public sealed class SeriesMilestonesTests
{
    private static readonly ResolvedProject Project = new() { Name = "sample" };
    private static readonly DateTimeOffset Now = SeriesRecords.T.AddMinutes(20);

    private static readonly SeriesTicketFact[] Before =
        [SeriesRecords.Fact("43", RunStatuses.Running, finished: false), SeriesRecords.Fact("44", RunStatuses.Running, finished: false), SeriesRecords.Fact("45")];

    private static SeriesRunRecord Seen(params SeriesAct[] acts) => SeriesRecords.Running() with
    {
        LastStatusAt = SeriesRecords.T, LastTable = SeriesTable.Snapshot(SeriesTable.Rows(Before, SeriesRecords.T)), Acts = acts,
    };

    private static SeriesBeat Beat(SeriesRunRecord record, params SeriesTicketFact[] facts) => new(record, facts, Now);

    [Fact]
    public void Merged_TwoTicketsMerged_OneSentenceWithTheCount()
    {
        var beat = Beat(Seen(), SeriesRecords.Fact("43", RunStatuses.Success, landed: Now),
            SeriesRecords.Fact("44", RunStatuses.Success, landed: Now), SeriesRecords.Fact("45"));

        SeriesReport.Messages(beat, Project).Should().Equal("SCRUM-43 and SCRUM-44 merged; 2 of 3 are in.");
    }

    [Fact]
    public void Merged_FirstBeat_SaysNothing()
    {
        var beat = Beat(SeriesRecords.Running() with { LastStatusAt = SeriesRecords.T },
            SeriesRecords.Fact("43", RunStatuses.Success, landed: Now));

        SeriesMilestones.Merged(beat).Should().BeNull("the first beat saw no table, so nothing changed");
    }

    [Fact]
    public void Merged_TheMasterMergedItBeatsAgo_IsNotToldAgain()
    {
        var act = new SeriesAct(SeriesRecords.T.AddMinutes(-30), SeriesPullRequestTools.MergeTool, "43", "merged SCRUM-43 (auto)", "green");
        var beat = Beat(Seen(act), SeriesRecords.Fact("43", RunStatuses.Success, landed: Now), SeriesRecords.Fact("44", RunStatuses.Success, landed: Now));

        SeriesMilestones.Merged(beat).Should().Be("SCRUM-44 merged; 2 of 2 are in.");
    }

    // 2026-11-25-96aeb: a merge the master was refused says nothing, so the later merge is told.
    [Fact]
    public void Merged_TheMastersMergeWasRefused_IsToldWhenItMerges()
    {
        var act = new SeriesAct(SeriesRecords.T.AddMinutes(-30), SeriesPullRequestTools.MergeTool, "43", "merge of SCRUM-43 refused", "green");
        var beat = Beat(Seen(act), SeriesRecords.Fact("43", RunStatuses.Success, landed: Now), SeriesRecords.Fact("44", RunStatuses.Success, landed: Now));

        SeriesMilestones.Merged(beat).Should().Be("SCRUM-43 and SCRUM-44 merged; 2 of 2 are in.");
    }

    [Fact]
    public void Stuck_ATicketTurnedRed_ToldAfterTheMastersLine()
    {
        var beat = Beat(Seen(), SeriesRecords.Fact("43", RunStatuses.Failed) with { EndDetail = "tests failed" });

        SeriesReport.Messages(beat, Project).Should().Equal("SCRUM-43 is stuck (red · tests failed) and waits for your decision.");
    }

    [Fact]
    public void Stuck_TheMasterActedOnItOrAsked_SaysNothing()
    {
        var red = SeriesRecords.Fact("43", RunStatuses.Failed);
        var retried = Beat(Seen(new SeriesAct(Now, "retry_ticket", "43", "retried SCRUM-43", "flaky")), red);
        var asked = Beat(Seen(new SeriesAct(Now, SeriesAskTool.Name, null, "asked", "which way")), red);

        SeriesMilestones.Stuck(retried).Should().BeEmpty("its act says what it did");
        SeriesMilestones.Stuck(asked).Should().BeEmpty("its question is in the chat");
    }

    [Fact]
    public void Stuck_ABudgetQuestionWasAskedThisBeat_SaysNothing()
    {
        var record = Seen() with { Question = new SeriesQuestion("q-1", "Raise the budget?", ["raise", "stop"], 0, Now, null) };

        SeriesMilestones.Stuck(Beat(record, SeriesRecords.Fact("43", RunStatuses.Failed))).Should().BeEmpty();
    }
}
