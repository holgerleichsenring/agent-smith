using AgentSmith.Application.Services.Holds;
using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using FluentAssertions;
using A = AgentSmith.Application.Services.Holds.TicketHoldAddressees;
using C = AgentSmith.Application.Services.Holds.TicketHoldCases;

namespace AgentSmith.Tests.Series;

/// <summary>2026-11-25-9992a: a series ticket's one kind — the Overview's colour and the master's table alike — read from
/// the hold's case, with a fallback on the newest attempt when nothing holds it.</summary>
public sealed class SeriesTicketStateTests
{
    [Theory]
    [InlineData(A.Merge, C.MergeNext, SeriesTicketKinds.Now)]
    [InlineData(A.Decide, C.Question, SeriesTicketKinds.Question)]
    [InlineData(A.Plan, C.TextEdited, SeriesTicketKinds.Question)]
    [InlineData(A.Admin, C.QuestionEnvironment, SeriesTicketKinds.Question)]
    [InlineData(A.Decide, C.CiFailed, SeriesTicketKinds.Failed)]
    [InlineData(A.Decide, C.ApprovalNeeded, SeriesTicketKinds.Approval)]
    [InlineData(A.Admin, C.ImageUntrusted, SeriesTicketKinds.Failed)]
    [InlineData(A.Nobody, C.Working, SeriesTicketKinds.Running)]
    [InlineData(A.Nobody, C.Refreshing, SeriesTicketKinds.Running)]
    [InlineData(A.Nobody, C.CiRunning, SeriesTicketKinds.Pr)]
    [InlineData(A.Nobody, C.Prerequisite, SeriesTicketKinds.Waiting)]
    public void KindOf_ReadsTheHoldsCase(string addressee, string @case, string kind)
    {
        var fact = SeriesRecords.Fact("43", RunStatuses.Success).Held(addressee, @case);
        if (@case is C.CiRunning) fact = fact.WithPr(PrReviewStates.Open, CiStates.Pending);

        SeriesTicketState.KindOf(fact).Should().Be(kind);
    }

    [Theory]
    [InlineData(C.CiRunning, SeriesTicketKinds.Pr)]
    [InlineData(C.ModelWait, SeriesTicketKinds.Running)]
    public void KindOf_AnEscalatedWait_KeepsItsCasesKind_AndSaysItIsOverdue(string @case, string kind)
    {
        // An escalated wait is the admin's, but the run or the CI still moves — the master must not read it as a failure.
        var fact = SeriesRecords.Fact("43", RunStatuses.Running, finished: false).WithPr(PrReviewStates.Open, CiStates.Pending)
            with { Hold = new TicketHold(A.Admin, @case, null, SeriesRecords.T) { Escalation = new TicketHoldEscalation(60, "holds.escalate_after_minutes") } };

        (fact.Kind, fact.State.EndsWith("overdue past 60 min (holds.escalate_after_minutes)", StringComparison.Ordinal))
            .Should().Be((kind, true));
    }

    [Fact]
    public void KindOf_ATicketOthersStartOn_IsNeeded_AWaitingPullRequestIsPr()
    {
        var pr = SeriesRecords.Fact("43", RunStatuses.Success).WithPr(PrReviewStates.Unread, null);

        (pr.Kind, (pr with { NeededBy = ["SCRUM-44"] }).Kind).Should().Be((SeriesTicketKinds.Pr, SeriesTicketKinds.Needed));
    }

    [Theory]
    [InlineData(null, SeriesTicketKinds.Waiting, "not started")]
    [InlineData(RunStatuses.Success, SeriesTicketKinds.Waiting, "green")]
    [InlineData(RunStatuses.Running, SeriesTicketKinds.Running, "coding")]
    [InlineData(RunStatuses.WaitingForInput, SeriesTicketKinds.Waiting, "waiting for input")]
    [InlineData("cancelled", SeriesTicketKinds.Failed, "red · cancelled")]
    public void KindOf_NothingHoldsIt_ReadsTheNewestAttempt(string? status, string kind, string words)
    {
        var fact = SeriesRecords.Fact("43", status);

        (fact.Kind, fact.State, fact.Working).Should().Be((kind, words, kind == SeriesTicketKinds.Running));
    }

    [Fact]
    public void KindOf_AParkedQuestionWithoutAHold_AsksAndLandedWinsOverEverything()
    {
        var asked = SeriesRecords.Fact("43", RunStatuses.WaitingForInput) with { PendingQuestion = new DialogQuestion("q-1", QuestionType.FreeText, "Which schema?", null, null, null, TimeSpan.FromDays(1)) };
        var landed = asked.Held(A.Decide, C.Failed) with { LandedAt = SeriesRecords.T };

        (asked.Kind, landed.Kind, landed.State).Should().Be((SeriesTicketKinds.Question, SeriesTicketKinds.Merged, "merged"));
    }

    // 2026-11-26-5201f: a pull request waiting for a person's approval is that person's step — not a failure, not CI — and its
    // words name the pull request; the master's table keeps the kind.
    [Fact]
    public void KindOf_AnApprovalNeeded_IsAPersonsApproval_WordedWithThePullRequest()
    {
        const string url = "https://dev.azure.com/o/p/_git/r/pullrequest/7";
        var fact = SeriesRecords.Fact("43", RunStatuses.Success).WithPr(PrReviewStates.Open, CiStates.Green) with
        {
            Hold = new TicketHold(A.Decide, C.ApprovalNeeded, new TicketHoldAct(TicketHoldActs.PullRequest, url), null),
        };

        (fact.Kind, fact.State, fact.Working).Should().Be((SeriesTicketKinds.Approval, $"needs a person's approval on the host · {url}", false));
        SeriesTable.Parse(SeriesTable.Snapshot([new SeriesTableRow("#43", "t", fact.State, fact.Kind, "$0", "")]))
            .Should().ContainKey("#43").WhoseValue.Kind.Should().Be(SeriesTicketKinds.Approval);
    }

    // Addendum: a draft is its own kind and words — never "publish" alone, since a run may keep one on purpose.
    [Fact]
    public void KindOf_APrDraft_IsDraft_WordedWithoutABlindPublish()
    {
        var fact = SeriesRecords.Fact("43", RunStatuses.Success).WithPr(PrReviewStates.Open, CiStates.Green) with
        {
            Hold = new TicketHold(A.Decide, C.PrDraft, new TicketHoldAct(TicketHoldActs.PullRequest, "https://host/pr/43/1"), null),
        };

        fact.Kind.Should().Be(SeriesTicketKinds.Draft);
        fact.State.Should().StartWith("PR is still a draft · the host asks nobody to approve it").And.Contain("only once its work is complete")
            .And.EndWith("· https://host/pr/43/1");
        SeriesTicketKinds.Is("draft").Should().BeTrue();
    }

    // Addendum: a stacked pull request is moved only by the master now — its words say so, and turning to it is a beat reason.
    [Fact]
    public void Retarget_TellsTheMasterToRetarget_AndIsABeatReasonOnce()
    {
        var fact = SeriesRecords.Fact("43", RunStatuses.Success) with
        {
            PullRequests = [new SeriesRunPullRequestView("api", "7", "https://host/pr/7", PrReviewStates.Open, CiStates.Green, null, null, "agent-smith/19654")],
            Hold = new TicketHold(A.Nobody, C.Retarget, null, null),
        };
        var record = SeriesRecords.Running();
        var beat = new SeriesBeat(record, [fact], SeriesRecords.T);

        fact.State.Should().Be("PR targets agent-smith/19654, neither the series nor the default branch · move it with retarget_pull_request");
        SeriesJudgement.NewlyRetarget(record, beat).Should().ContainSingle().Which.Should().StartWith("SCRUM-43 needs a retarget");
        SeriesJudgement.NewlyRetarget(record with { LastTable = beat.Table }, beat).Should().BeEmpty("the last table already said it");
        TicketHoldEscalations.Escalating(C.Retarget).Should().BeFalse("only the master moves it, nothing is overdue");
    }

    // 2026-11-26-5201f: the master reads the approval apart from CI — the ticket's kind and each pull request's flag.
    [Fact]
    public async Task ReadTicket_CarriesTheApprovalApartFromCi()
    {
        var fact = SeriesRecords.Fact("43", RunStatuses.Success) with
        {
            PullRequests = [new SeriesRunPullRequestView("api", "7", "https://host/pr/7", PrReviewStates.Open, CiStates.Green, null, null, "main") { ApprovalRequired = true, IsDraft = false }],
            Hold = new TicketHold(A.Decide, C.ApprovalNeeded, new TicketHoldAct(TicketHoldActs.PullRequest, "https://host/pr/7"), null),
        };
        var tools = new SeriesReadTools(new SeriesBeat(SeriesRecords.Running(), [fact], SeriesRecords.T));

        var json = await tools.ReadTicketAsync("SCRUM-43");

        json.Should().Contain("\"approvalRequired\":true").And.Contain("\"isDraft\":false").And.Contain("\"kind\":\"approval\"").And.Contain("\"ci\":\"green\"");
        tools.SeriesStatus().Should().Contain("\"hold\":\"approval_needed\"");
    }

    [Fact]
    public void SeriesTicketKinds_Is_KnowsEveryKindAndNothingElse()
    {
        new[] { "merged", "now", "needed", "question", "approval", "draft", "failed", "running", "pr", "waiting" }.Should().OnlyContain(k => SeriesTicketKinds.Is(k));
        new[] { "landed", "red", "held", "", null }.Should().NotContain(k => SeriesTicketKinds.Is(k));
    }

    [Fact]
    public void SeriesRunCounts_Of_CountsTheKinds()
    {
        IReadOnlyList<SeriesTicketFact> facts =
        [
            SeriesRecords.Fact("43", RunStatuses.Success, landed: SeriesRecords.T),
            SeriesRecords.Fact("44", RunStatuses.Success).Held(A.Merge, C.MergeNext),
            SeriesRecords.Fact("45", RunStatuses.Failed).Held(A.Decide, C.Failed),
            SeriesRecords.Fact("46"),
            SeriesRecords.Fact("47", RunStatuses.Running, finished: false).Held(A.Nobody, C.Working),
        ];

        AgentSmith.Server.Models.SeriesRunCounts.Of(facts).Should().Be(new AgentSmith.Server.Models.SeriesRunCounts(5, 1, 1, 1, 1));
    }

    [Fact]
    public void SeriesWorkingOn_NamesTheTicketsARunIsAtWorkOn()
    {
        IReadOnlyList<SeriesTicketFact> facts =
        [
            SeriesRecords.Fact("43", RunStatuses.Running, finished: false).Held(A.Nobody, C.Working),
            SeriesRecords.Fact("44", RunStatuses.Success).Held(A.Merge, C.MergeNext),
            SeriesRecords.Fact("45", RunStatuses.Running, finished: false).Held(A.Nobody, C.ModelWait),
        ];

        SeriesWorkingOn.Of(facts).Should().Equal("SCRUM-43", "SCRUM-45");
    }
}
