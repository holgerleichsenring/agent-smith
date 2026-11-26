using AgentSmith.Application.Services.Holds;
using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Series;
using FluentAssertions;

namespace AgentSmith.Tests.Series;

/// <summary>2026-11-17-0207b: the series opens with what it filed, started and waits on and its policy; 2026-11-25-e94eb: no table is
/// posted; the beat's own words and the master's line are.</summary>
public sealed class SeriesReportTests
{
    private static readonly ResolvedProject Project = new()
    {
        Name = "sample",
    };

    private static readonly DialogueGlobalConfig Dialogue = new();

    private static SeriesTicketFact[] Facts() =>
        [SeriesRecords.Fact("43", RunStatuses.Running, finished: false).Held(TicketHoldAddressees.Nobody, TicketHoldCases.Working),
         SeriesRecords.Fact("44", requires: "43").Held(TicketHoldAddressees.Nobody, TicketHoldCases.Prerequisite, null, "SCRUM-43"),
         SeriesRecords.Fact("45", requires: "43").Held(TicketHoldAddressees.Nobody, TicketHoldCases.Prerequisite, null, "SCRUM-43")];

    // 2026-11-25-e94eb: every series conversation opens the same way and gets no table — the series page is the status.
    [Fact]
    public void FirstBeat_OpensWithWhatStartedWhatWaitsAndThePolicy_AndNoTable()
    {
        var record = SeriesRecords.Running() with { Review = ReviewMode.None }; // 2026-11-25-9992i
        var messages = SeriesReport.Messages(new SeriesBeat(record, Facts(), SeriesRecords.T), Project);

        messages.Should().ContainSingle().Which.Should().Be("Filed 3 tickets. SCRUM-43 started; 2 wait on their prerequisites and "
            + "start as soon as the ticket they need has a green push. I merge each one when its build and tests are green. I write here only "
            + "when you are needed; the series page on the dashboard shows every ticket, and asking me to hold pauses the series.");
    }

    // 2026-11-24-f9c5c: a series with a branch says it merges its tickets there, and waits on landings; 2026-11-25-9992i: a series
    // opened before the review setting reads as end with its branch.
    [Fact]
    public void FirstBeat_ASeriesWithABranch_StatesTheSeriesRule()
    {
        var record = SeriesRecords.Running() with { IntegrationBranch = "agent-smith/series/sr-1" };

        var messages = SeriesReport.Messages(new SeriesBeat(record, Facts(), SeriesRecords.T), Project);

        messages.Should().ContainSingle().Which.Should().Contain("start once the ticket they need is merged")
            .And.Contain("I merge each one into agent-smith/series/sr-1 when its build and tests are green")
            .And.Contain("the series reaches the default branch as one pull request per repository at the end, for you to review");
    }

    // 2026-11-25-9992i: a series under review: each says a person approves each pull request first.
    [Fact]
    public void FirstBeat_AnEachSeries_SaysItMergesAfterYourApproval()
    {
        var record = SeriesRecords.Running() with { Review = ReviewMode.Each };

        SeriesReport.Messages(new SeriesBeat(record, Facts(), SeriesRecords.T), Project).Should().ContainSingle()
            .Which.Should().Contain("I merge each one once you approved its pull request");
    }

    [Fact]
    public void LaterBeat_PostsWhatItSaidAndTheMastersLine()
    {
        var beat = new SeriesBeat(SeriesRecords.Running(), Facts(), SeriesRecords.T.AddMinutes(10));
        beat.Record = beat.Record with { LastStatusAt = SeriesRecords.T, LastTable = beat.Table };
        beat.Said.Add("SCRUM-44 asks a decision only you can make: …");
        beat.MasterLine = "Passed to SCRUM-44.";
        beat.MasterActed = true; // 2026-11-25-e94ec: the line of a master that acted

        SeriesReport.Messages(beat, Project).Should().Equal("SCRUM-44 asks a decision only you can make: …", "Passed to SCRUM-44.");
    }

    // 2026-11-26-f511c: an act and the master's narration of it are one entry — the sentence, and the beat's acts as one line under it.
    [Fact]
    public void LaterBeat_TheMastersLine_CarriesTheBeatsActsAsOneLine_NoNoticeBesideIt()
    {
        var beat = new SeriesBeat(SeriesRecords.Running(), Facts(), SeriesRecords.T.AddMinutes(10));
        beat.Record = beat.Record with { LastStatusAt = SeriesRecords.T, LastTable = beat.Table };
        SeriesActSay.Acted(beat, "refreshed SCRUM-44", "it conflicts with develop", "ok");
        SeriesActSay.Acted(beat, "started the CI of SCRUM-45", null, "ok");
        beat.MasterLine = "SCRUM-44 is brought up to date with develop.";
        beat.MasterActed = true;

        SeriesReport.Messages(beat, Project).Should().Equal(
            "SCRUM-44 is brought up to date with develop.\n\n↳ refreshed SCRUM-44 · started the CI of SCRUM-45");
    }

    [Fact]
    public void LaterBeat_NoMastersLine_PostsEachActsNotice_AReasonlessActNone()
    {
        var beat = new SeriesBeat(SeriesRecords.Running(), Facts(), SeriesRecords.T.AddMinutes(10));
        beat.Record = beat.Record with { LastStatusAt = SeriesRecords.T, LastTable = beat.Table };
        SeriesActSay.Acted(beat, "refreshed SCRUM-44", "it conflicts with develop", "ok");
        SeriesActSay.Acted(beat, "started the CI of SCRUM-45", null, "ok");
        SeriesActSay.Told(beat, "merged SCRUM-43 (now) over its refusal", "ok");
        beat.MasterActed = true;

        SeriesReport.Messages(beat, Project).Should().Equal(
            "agent-smith refreshed SCRUM-44: it conflicts with develop", "agent-smith merged SCRUM-43 (now) over its refusal");
    }

    [Fact]
    public void LaterBeat_TableChangedAndCadencePassed_PostsNothing()
    {
        var beat = new SeriesBeat(SeriesRecords.Running(), [SeriesRecords.Fact("43", RunStatuses.Success), .. Facts()[1..]],
            SeriesRecords.T.AddMinutes(45));
        beat.Record = beat.Record with { LastStatusAt = SeriesRecords.T, LastTable = "SCRUM-43=coding" };

        SeriesReport.Messages(beat, Project).Should().BeEmpty("the series page shows every ticket's state");
        SeriesReport.Seen(beat.Record, beat).LastStatusAt.Should().Be(SeriesRecords.T.AddMinutes(45), "a quiet beat still stamps when it ran");
    }

    [Fact]
    public void ATicketWaitingForItsModelPlace_ReadsAsRunning()
    {
        // 2026-11-22-d434b: a run waiting for its model place is another run's turn, not a stall.
        SeriesTicketFact[] facts = [Facts()[0].Held(AgentSmith.Application.Services.Holds.TicketHoldAddressees.Nobody,
            AgentSmith.Application.Services.Holds.TicketHoldCases.ModelWait), .. Facts()[1..]];

        (facts[0].State, facts[0].Kind).Should().Be(("waiting for its model place", SeriesTicketKinds.Running));
    }

    [Fact]
    public void Policy_Line_NamesMergeStartAndQuestionDefault() =>
        SeriesPolicy.Line(Project, Dialogue, ReviewMode.None).Should().Be(
            "merge on green · start on green push · questions default to the recommendation after 2 h 00 min");

    // 2026-11-25-9992i
    [Theory]
    [InlineData(ReviewMode.End, "you review the final pull request")]
    [InlineData(ReviewMode.Each, "you approve each pull request")]
    [InlineData(ReviewMode.None, "merge on green")]
    public void Policy_Merge_NamesTheReview(ReviewMode review, string said) => SeriesPolicy.Merge(review).Should().Be(said);
}
