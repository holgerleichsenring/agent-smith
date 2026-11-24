using AgentSmith.Application.Services.Holds;
using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Tickets;
using AgentSmith.Server.Services.Tickets.Holds;
using FluentAssertions;

namespace AgentSmith.Tests.Tickets.Holds;

/// <summary>2026-11-23-d60d: an open pull request always holds — one that can merge now is a merge hold (through the series'
/// merge in a series, by opening the pull request outside one), one that waits is nobody's with its reason and since, red
/// CI is a person's decision naming the failed checks.</summary>
public sealed class TicketOpenPullRequestHoldTests
{
    private static readonly DateTimeOffset T = DateTimeOffset.Parse("2026-11-23T09:00:00Z");
    private const string Url = "https://github.com/o/api/pull/42";
    private readonly TicketHoldReader _reader = new();

    private static TicketHoldFacts Facts(TicketOpenPullRequests open, string status = RunStatuses.Success) =>
        new("alpha", "42") { HeadStatus = status, HeadRunId = "r-2", Attempt = 2, EndedAt = T, OpenPullRequests = open };

    private static TicketOpenPullRequests Open(string? ci, DateTimeOffset? since = null) => new(ci, since) { Url = Url };

    [Fact]
    public void Of_AGreenPullRequestOutsideASeries_IsAMergeHoldThatOpensThePullRequest()
    {
        var hold = _reader.Of(Facts(Open(CiStates.Green, T.AddMinutes(5))))!;

        hold.Should().BeEquivalentTo(new TicketHold(TicketHoldAddressees.Merge, TicketHoldCases.MergeReady,
            new TicketHoldAct(TicketHoldActs.PullRequest, Url), T.AddMinutes(5)) { Attempt = 2 });
        _reader.Of(Facts(Open(null)))!.Should().Match<TicketHold>(h => h.Case == TicketHoldCases.MergeReady && h.Since == T,
            "no CI read and no CI reporting can merge too, since the attempt ended");
        _reader.Of(Facts(Open(CiStates.None)))!.Case.Should().Be(TicketHoldCases.MergeReady);
    }

    // 2026-11-23-0219: a required build that has not started is its own wait, apart from one that runs, and escalates alike.
    [Fact]
    public void Of_ABuildNotStarted_IsNobodysCiNotStarted_NotAMerge()
    {
        var hold = _reader.Of(Facts(Open(CiStates.NotStarted, T.AddMinutes(3))))!;

        (hold.Addressee, hold.Case, hold.Since).Should().Be(
            (TicketHoldAddressees.Nobody, TicketHoldCases.CiNotStarted, (DateTimeOffset?)T.AddMinutes(3)));
        TicketHoldEscalations.Escalating(TicketHoldCases.CiNotStarted).Should().BeTrue();
        TicketPullRequestHold.CiHolds(Facts(Open(CiStates.NotStarted))).Should().BeTrue();
    }

    [Fact]
    public void Of_ASeriesPullRequestNextInOrder_KeepsTheSeriesMerge()
    {
        var hold = _reader.Of(Facts(Open(CiStates.Green)) with
        {
            SeriesId = "sr-1", MergeStep = new SeriesRunMergeStep("42", "SCRUM-42", 0, SeriesRunMergeWaits.Ready, []),
        })!;

        (hold.Addressee, hold.Case, hold.Act).Should().Be((TicketHoldAddressees.Merge, TicketHoldCases.MergeNext,
            new TicketHoldAct(TicketHoldActs.Merge, "sr-1")));
    }

    [Fact]
    public void Of_APullRequestBehindAnUnlandedRequirementOrWithCiRunning_IsNobodysWithItsReason()
    {
        var behind = _reader.Of(Facts(Open(CiStates.Green) with { Unlanded = ["#41"] }))!;
        var running = _reader.Of(Facts(Open(CiStates.Pending, T.AddMinutes(3))))!;
        var seriesRunning = _reader.Of(Facts(Open(CiStates.Pending, T.AddMinutes(3))) with
        {
            SeriesId = "sr-1", MergeStep = new SeriesRunMergeStep("42", "SCRUM-42", 0, SeriesRunMergeWaits.Ready, []),
        })!;

        (behind.Addressee, behind.Case, behind.Since).Should().Be((TicketHoldAddressees.Nobody, TicketHoldCases.MergeAfter, (DateTimeOffset?)T));
        behind.Names.Should().Equal("#41");
        (running.Addressee, running.Case, running.Since).Should().Be(
            (TicketHoldAddressees.Nobody, TicketHoldCases.CiRunning, (DateTimeOffset?)T.AddMinutes(3)));
        seriesRunning.Case.Should().Be(TicketHoldCases.CiRunning, "a series' next merge waits for its CI as well");
    }

    [Fact]
    public void Of_RedCi_IsAPersonsDecisionNamingTheFailedChecks_ALiveAttemptIsNot()
    {
        var red = _reader.Of(Facts(Open(CiStates.Red, T.AddMinutes(7)) with { FailedChecks = ["build", "lint"] }))!;

        (red.Addressee, red.Case, red.Since, red.Act).Should().Be((TicketHoldAddressees.Decide, TicketHoldCases.CiFailed,
            (DateTimeOffset?)T.AddMinutes(7), new TicketHoldAct(TicketHoldActs.PullRequest, Url)));
        red.Names.Should().Equal("build", "lint");
        _reader.Of(Facts(Open(CiStates.Red), RunStatuses.Running))!.Case.Should().Be(TicketHoldCases.Working,
            "a fix that runs is the attempt's work");
        _reader.Of(Facts(Open(CiStates.Red), RunStatuses.Failed))!.Case.Should().Be(TicketHoldCases.Failed,
            "a failed attempt is the decision, its pull requests or not");
    }

    [Fact]
    public void OpenPullRequests_Of_RedWinsOverPending_ACiReadForAnotherHeadCountsNothing_TheUrlOnlyForOne()
    {
        var red = Record("1", "a", new PrRecordCiView(CiStates.Red, "a", T.AddMinutes(2), [new CiFailure("build", null, "x")]));
        var pending = Record("2", "b", new PrRecordCiView(CiStates.Pending, "b", T, []));
        var stale = Record("3", "c", new PrRecordCiView(CiStates.Red, "old", T, [new CiFailure("lint", null, "y")])) with { HeadMovedAt = T.AddMinutes(9) };
        var unread = Record("4", "d", null);

        var both = TicketOpenPullRequestsReader.Of([red, pending, stale]);
        var one = TicketOpenPullRequestsReader.Of([stale]);
        var never = TicketOpenPullRequestsReader.Of([unread]);

        (both.Ci, both.CiSince, both.Url).Should().Be((CiStates.Red, (DateTimeOffset?)T.AddMinutes(2), (string?)null));
        both.FailedChecks.Should().Equal("build");
        (one.Ci, one.CiSince, one.Url).Should().Be((CiStates.Pending, (DateTimeOffset?)T.AddMinutes(9), "https://github.com/o/api/pull/3"),
            "a read for an older head is the new head's CI not read yet — no merge before it is");
        one.FailedChecks.Should().BeEmpty();
        never.Ci.Should().BeNull("no CI read at all is a repository without CI");
    }

    [Fact]
    public void Of_AnInterruptionTheRestartTakesUp_IsRestarting_ItsPullRequestHoldsNothing()
    {
        var facts = Facts(Open(CiStates.Green), "cancelled") with { Interrupted = true, Restarting = true };

        _reader.Of(facts)!.Case.Should().Be(TicketHoldCases.Restarting);
        _reader.Of(facts with { OpenPullRequests = Open(CiStates.Red) })!.Case.Should().Be(TicketHoldCases.Restarting);
        _reader.Of(facts with { Restarting = false })!.Case.Should().Be(TicketHoldCases.Interrupted, "one the restart leaves is a decision");
    }

    [Fact]
    public void NodeState_AWorkingTicketWhoseAttemptEnded_IsWaiting_ALiveOneIsRunning()
    {
        TicketSummaryView Row(string status) =>
            new("alpha", "42", null, null, null, TicketBucket.Working, null, T, 1, 0m, "r-2", status, null, null, null, 0, 0, [], []);

        TicketNodeStates.Of(null, Row(RunStatuses.Success)).Should().Be(TicketNodeStates.Waiting);
        TicketNodeStates.Of(null, Row(RunStatuses.Running)).Should().Be(TicketNodeStates.Running);
        TicketNodeStates.Of(null, Row(RunStatuses.Queued)).Should().Be(TicketNodeStates.Running);
    }

    private static PrReviewRecordView Record(string number, string head, PrRecordCiView? ci) =>
        new($"pr-{number}", $"https://github.com/o/api/pull/{number}", "api", number, "alpha", "42", "r-2", PrReviewStates.Open,
            T, T, null, []) { HeadSha = head, Ci = ci };

    // 2026-11-24-6811a: a conflict holds for a person whatever the CI says, since nothing merges it; at work it holds nothing.
    [Theory]
    [InlineData(CiStates.Green)]
    [InlineData(CiStates.Pending)]
    [InlineData(CiStates.Red)]
    public void Of_AConflictingPullRequest_IsAPersonsPrConflict_NotAMergeOrACiWait(string ci)
    {
        var open = Open(ci, T.AddMinutes(1)) with { Conflicts = true, ConflictsSince = T.AddMinutes(4) };

        var hold = _reader.Of(Facts(open))!;

        (hold.Addressee, hold.Case, hold.Act, hold.Since).Should().Be((TicketHoldAddressees.Decide, TicketHoldCases.PrConflict,
            new TicketHoldAct(TicketHoldActs.PullRequest, Url), (DateTimeOffset?)T.AddMinutes(4)));
        _reader.Merge(Facts(open)).Should().BeNull();
        _reader.Of(Facts(open, RunStatuses.Running))!.Case.Should().Be(TicketHoldCases.Working);
    }

    [Fact]
    public void Of_OpenPullRequestsReader_ConflictsWhenAnyOpenRecordDoes_SinceTheEarliest()
    {
        var a = Record("https://h/1") with { HasConflicts = true, ConflictsAt = T.AddMinutes(9) };
        var b = Record("https://h/2") with { HasConflicts = true, ConflictsAt = T.AddMinutes(2) };

        var open = TicketOpenPullRequestsReader.Of([a, b, Record("https://h/3")]);

        (open.Conflicts, open.ConflictsSince).Should().Be((true, (DateTimeOffset?)T.AddMinutes(2)));
        TicketOpenPullRequestsReader.Of([Record("https://h/3")]).Conflicts.Should().BeFalse();
    }

    private static PrReviewRecordView Record(string url) =>
        new(PrKey.Of(url), url, "api", "1", "alpha", "42", "r-2", PrReviewStates.Open, T, T, null, []);
}
