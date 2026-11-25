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

    [Fact]
    public void SeriesTicketKinds_Is_KnowsEveryKindAndNothingElse()
    {
        new[] { "merged", "now", "needed", "question", "failed", "running", "pr", "waiting" }.Should().OnlyContain(k => SeriesTicketKinds.Is(k));
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
