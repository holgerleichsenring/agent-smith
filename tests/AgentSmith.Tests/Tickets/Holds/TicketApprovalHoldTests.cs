using AgentSmith.Application.Services.Holds;
using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Server.Services.Tickets.Holds;
using FluentAssertions;

namespace AgentSmith.Tests.Tickets.Holds;

/// <summary>
/// 2026-11-26-5201f: an open pull request whose host still needs a person's approval holds as approval_needed — a person's
/// step with the pull request's link, before a merge and before CI that runs, never a wait that turns overdue. The reported
/// case: the build passed (or there is none), a reviewer policy waits, and the ticket read as CI running for days.
/// </summary>
public sealed class TicketApprovalHoldTests
{
    private static readonly DateTimeOffset T = DateTimeOffset.Parse("2026-11-22T20:17:00Z");
    private const string Url = "https://dev.azure.com/o/p/_git/r/pullrequest/7";
    private readonly TicketHoldReader _reader = new();

    private static TicketHoldFacts Facts(TicketOpenPullRequests open, string status = RunStatuses.Success) =>
        new("alpha", "19656") { HeadStatus = status, HeadRunId = "r-2", Attempt = 1, EndedAt = T, OpenPullRequests = open,
            EscalateAfterMinutes = 60, Now = T.AddDays(4) };

    private static TicketOpenPullRequests Awaiting(string? ci, DateTimeOffset? since = null) =>
        new(ci, since) { Url = Url, AwaitingApproval = [Url] };

    [Theory]
    [InlineData(CiStates.Green)]
    [InlineData(CiStates.None)]
    [InlineData(null)]
    [InlineData(CiStates.Pending)]
    [InlineData(CiStates.NotStarted)]
    public void Of_ApprovalRequired_IsAPersonsApprovalNeeded_NotCiRunningOrAMerge(string? ci)
    {
        var hold = _reader.Of(Facts(Awaiting(ci, T)))!;

        hold.Should().BeEquivalentTo(new TicketHold(TicketHoldAddressees.Decide, TicketHoldCases.ApprovalNeeded,
            new TicketHoldAct(TicketHoldActs.PullRequest, Url), null) { Attempt = 1 });
        hold.Escalation.Should().BeNull("a person's step is never reported overdue");
        hold.IsPersons.Should().BeTrue();
        _reader.Merge(Facts(Awaiting(ci))).Should().BeNull("nothing merges before the approval");
    }

    [Fact]
    public void Of_ApprovalRequired_InASeriesNextInOrder_IsStillTheApproval()
    {
        var hold = _reader.Of(Facts(Awaiting(CiStates.Green)) with
        {
            SeriesId = "sr-1", MergeStep = new SeriesRunMergeStep("19656", "#19656", 0, SeriesRunMergeWaits.Ready, []),
        })!;

        hold.Case.Should().Be(TicketHoldCases.ApprovalNeeded);
    }

    [Fact]
    public void Of_RedCiOrAConflict_WinsOverTheApproval_AnAttemptAtWorkHoldsNeither()
    {
        _reader.Of(Facts(Awaiting(CiStates.Red)))!.Case.Should().Be(TicketHoldCases.CiFailed);
        _reader.Of(Facts(Awaiting(CiStates.Green) with { Conflicts = true }))!.Case.Should().Be(TicketHoldCases.PrConflict);
        _reader.Of(Facts(Awaiting(CiStates.Green), RunStatuses.Running))!.Case.Should().Be(TicketHoldCases.Working);
        TicketHostStepHold.ApprovalNeeded(Facts(Awaiting(CiStates.Green), RunStatuses.Running)).Should().BeNull();
    }

    [Fact]
    public void Of_NoApprovalRequired_CiPendingStaysCiRunning_AndEscalates()
    {
        var hold = _reader.Of(Facts(new TicketOpenPullRequests(CiStates.Pending, T) { Url = Url }))!;

        (hold.Case, hold.Addressee).Should().Be((TicketHoldCases.CiRunning, TicketHoldAddressees.Admin));
        TicketHostStepHold.ApprovalNeeded(Facts(new TicketOpenPullRequests(CiStates.Green, T))).Should().BeNull();
    }

    [Fact]
    public void OpenPullRequestsReader_NamesTheOpenOnesAwaitingApproval_FirstIsTheAct()
    {
        var waits = Record("https://h/2") with { ApprovalRequired = true };

        var open = TicketOpenPullRequestsReader.Of([Record("https://h/1"), waits]);

        open.AwaitingApproval.Should().Equal("https://h/2");
        _reader.Of(Facts(open))!.Act.Should().Be(new TicketHoldAct(TicketHoldActs.PullRequest, "https://h/2"), "it names the one that waits");
        TicketOpenPullRequestsReader.Of([Record("https://h/1")]).AwaitingApproval.Should().BeEmpty();
    }

    // Addendum: a draft is asked of nobody by the host — its own step, before an approval and every CI wait, after a conflict
    // and red CI; at work, or at the series' retarget step, it is not the hold.
    [Theory]
    [InlineData(CiStates.Green)]
    [InlineData(CiStates.Pending)]
    [InlineData(CiStates.NotStarted)]
    [InlineData(null)]
    public void Of_AnOpenDraft_IsAPersonsPrDraft_NotCiOrApproval(string? ci)
    {
        var hold = _reader.Of(Facts(new TicketOpenPullRequests(ci, T) { Url = Url, Drafts = [Url], AwaitingApproval = [Url] }))!;

        hold.Should().BeEquivalentTo(new TicketHold(TicketHoldAddressees.Decide, TicketHoldCases.PrDraft,
            new TicketHoldAct(TicketHoldActs.PullRequest, Url), null) { Attempt = 1 });
        hold.Escalation.Should().BeNull();
    }

    [Fact]
    public void Of_ADraft_LosesToRedCiAConflictWorkAndARetarget()
    {
        var draft = new TicketOpenPullRequests(CiStates.Red, T) { Url = Url, Drafts = [Url] };
        _reader.Of(Facts(draft))!.Case.Should().Be(TicketHoldCases.CiFailed);
        _reader.Of(Facts(draft with { Ci = CiStates.Green, Conflicts = true }))!.Case.Should().Be(TicketHoldCases.PrConflict);
        _reader.Of(Facts(draft with { Ci = CiStates.Green }, RunStatuses.Running))!.Case.Should().Be(TicketHoldCases.Working);
        _reader.Of(Facts(draft with { Ci = CiStates.Green }) with
        {
            SeriesId = "sr-1", MergeStep = new SeriesRunMergeStep("19656", "#19656", 0, SeriesRunMergeWaits.Retarget, []),
        })!.Case.Should().Be(TicketHoldCases.Retarget, "a stacked draft is moved first; the merge would refuse it");
        TicketOpenPullRequestsReader.Of([Record("https://h/1") with { IsDraft = true }, Record("https://h/2")]).Drafts.Should().Equal("https://h/1");
    }

    private static PrReviewRecordView Record(string url) =>
        new(PrKey.Of(url), url, "api", "1", "alpha", "19656", "r-2", PrReviewStates.Open, T, T, null, []);
}
