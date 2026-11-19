using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Series;
using FluentAssertions;

namespace AgentSmith.Tests.Series;

/// <summary>2026-11-19-1b72a: the lines a beat writes under a Series pane — once per change that needs the operator, never
/// repeated while the state holds, and a red ticket left to the master's line when the master speaks.</summary>
public sealed class SeriesNeedsYouTests
{
    private static readonly ResolvedProject Manual = new() { Name = "sample", Merge = new MergePolicyConfig { When = MergeWhen.Manual } };

    private static SeriesBeat Beat(string lastTable, params SeriesTicketFact[] facts) =>
        new(SeriesRecords.Running() with { LastTable = lastTable }, facts, SeriesRecords.T.AddMinutes(20));

    private static SeriesTicketFact Green(string ci) => SeriesRecords.Fact("43", RunStatuses.Success) with
    {
        OpenedPullRequest = true, CiState = ci, CiFailure = ci == CiStates.Red ? "build failed" : null,
    };

    [Fact]
    public void NeedsYou_UnchangedState_IsNotRepeatedAcrossBeats()
    {
        var first = Beat(string.Empty, SeriesRecords.Fact("43", RunStatuses.Failed));
        var second = Beat(first.Table, SeriesRecords.Fact("43", RunStatuses.Failed));

        SeriesNeedsYou.Lines(first, Manual).Should().Equal(["SCRUM-43 failed."], "the status is not said twice");
        SeriesNeedsYou.Lines(second, Manual).Should().BeEmpty();
    }

    [Fact]
    public void NeedsYou_GreenThenCiRedThenGreen_SaysTheMergeOnceAndTheFailure()
    {
        var pending = Beat(string.Empty, Green(CiStates.Pending));
        var red = Beat(pending.Table, Green(CiStates.Red));
        var green = Beat(red.Table, Green(CiStates.Green));
        var greener = Beat(green.Table, Green(CiStates.Green));

        SeriesNeedsYou.Lines(pending, Manual).Should().Equal("SCRUM-43's pull request waits for your merge.");
        SeriesNeedsYou.Lines(red, Manual).Should().Equal("SCRUM-43's CI is red: build failed.");
        SeriesNeedsYou.Lines(green, Manual).Should().Equal("SCRUM-43's pull request waits for your merge.");
        SeriesNeedsYou.Lines(greener, Manual).Should().BeEmpty();
    }

    [Fact]
    public void NeedsYou_MergedByTheSeries_SaysNoMergeLine()
    {
        var project = new ResolvedProject { Name = "sample", Merge = new MergePolicyConfig { When = MergeWhen.OnGateGreen } };

        SeriesNeedsYou.Lines(Beat(string.Empty, Green(CiStates.Green)), project).Should().BeEmpty();
    }

    [Fact]
    public void NeedsYou_RedWhileTheMasterSpeaks_LeavesTheFailureToTheMaster()
    {
        var beat = Beat(string.Empty, SeriesRecords.Fact("43", RunStatuses.Failed));
        beat.MasterLine = "SCRUM-43 failed on a flaky check; I retried it.";

        SeriesNeedsYou.Lines(beat, Manual).Should().BeEmpty();
    }

    [Fact]
    public void NeedsYou_Question_IsSaid()
    {
        SeriesNeedsYou.Lines(Beat(string.Empty, SeriesRecords.Fact("43", RunStatuses.WaitingForInput, finished: false)), Manual)
            .Should().Equal("SCRUM-43 waits for your input on its ticket.");
    }

    [Fact]
    public void NeedsYou_PendingQuestion_IsQuotedInItsOwnWords()
    {
        var fact = SeriesRecords.Fact("43", RunStatuses.WaitingForInput, finished: false) with
        {
            PendingQuestion = new AgentSmith.Contracts.Dialogue.DialogQuestion("q-1", default, "Keep the original deadline?", null, null, null, TimeSpan.Zero),
        };

        SeriesNeedsYou.Lines(Beat(string.Empty, fact), Manual).Should().Equal("SCRUM-43 asks: Keep the original deadline?");
    }

    [Fact]
    public void NeedsYou_RedCiUnderOnCiGreen_IsTheLoopsToFix()
    {
        var project = new ResolvedProject { Name = "sample", Merge = new MergePolicyConfig { When = MergeWhen.OnCiGreen } };

        SeriesNeedsYou.Lines(Beat("SCRUM-43=green · CI running", Green(CiStates.Red)), project).Should().BeEmpty();
    }

    [Fact]
    public void NeedsYou_FailureWithAReason_SaysTheReason()
    {
        SeriesNeedsYou.Lines(Beat(string.Empty, SeriesRecords.Fact("43", RunStatuses.Failed) with { EndDetail = "tests failed" }), Manual)
            .Should().Equal("SCRUM-43 failed: tests failed.");
    }
}
