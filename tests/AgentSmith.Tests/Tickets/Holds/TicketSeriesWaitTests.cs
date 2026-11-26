using AgentSmith.Application.Services.Holds;
using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Series;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Tickets;
using FluentAssertions;

namespace AgentSmith.Tests.Tickets.Holds;

/// <summary>
/// 2026-11-26-f511a: a ticket of an active series that waits on another of its tickets is no person's step yet — a draft stacked
/// on a requirement that has not landed, and a failure its master took up (tell, refresh, retarget) while a requirement is still
/// worked. Both read as a prerequisite wait on nobody, Waits on another; without the wait, or untaken, they stay a person's.
/// </summary>
public sealed class TicketSeriesWaitTests
{
    private static readonly DateTimeOffset T = DateTimeOffset.Parse("2026-11-26T09:00:00Z");
    private const string Url = "https://git.test/server/pull/9970";
    private readonly TicketHoldReader _reader = new();
    private readonly TicketBucketRule _buckets = new();

    private static TicketHoldFacts Draft(params string[] unlanded) =>
        new("shop", "19670") { HeadStatus = RunStatuses.Success, HeadRunId = "r-1", Attempt = 1, EndedAt = T,
            OpenPullRequests = new TicketOpenPullRequests(CiStates.Green, T) { Url = Url, Drafts = [Url] }, SeriesUnlanded = unlanded };

    private static TicketHoldFacts Failed(DateTimeOffset? takenUp, params string[] unlanded) =>
        new("shop", "19664") { HeadStatus = RunStatuses.Failed, HeadRunId = "r-2", Attempt = 2, EndedAt = T,
            SeriesUnlanded = unlanded, SeriesTakenUpAt = takenUp };

    [Fact]
    public void Of_ADraftStackedOnAnUnlandedRequirement_WaitsOnAnother_NotAPersonsDraft()
    {
        var hold = _reader.Of(Draft("#19668"))!;

        (hold.Addressee, hold.Case).Should().Be((TicketHoldAddressees.Nobody, TicketHoldCases.Prerequisite));
        hold.Names.Should().Equal("#19668");
        _reader.Of(Draft())!.Case.Should().Be(TicketHoldCases.PrDraft, "with its requirements landed the draft is a person's step");
    }

    [Fact]
    public void Of_AFailureTheMasterTookUp_WhileARequirementIsWorked_WaitsOnAnother()
    {
        var hold = _reader.Of(Failed(T.AddMinutes(5), "#19694"))!;

        (hold.Addressee, hold.Case).Should().Be((TicketHoldAddressees.Nobody, TicketHoldCases.Prerequisite));
        hold.Names.Should().Equal("#19694");
    }

    [Fact]
    public void Of_AFailureUntaken_OrTakenUpBeforeItFailed_OrWithNothingUnlanded_StaysThePersons()
    {
        _reader.Of(Failed(null, "#19694"))!.Case.Should().Be(TicketHoldCases.Failed);
        _reader.Of(Failed(T.AddMinutes(-5), "#19694"))!.Case.Should().Be(TicketHoldCases.Failed, "the act came before this failure");
        _reader.Of(Failed(T.AddMinutes(5)))!.Case.Should().Be(TicketHoldCases.Failed, "nothing it waits on is left");
    }

    [Fact]
    public void Held_APrerequisiteWait_IsWaitsOnAnother_WhateverTheAttemptLeft()
    {
        var failed = new TicketBucketState(TicketBucket.Failed, null, T, RunStatuses.Failed, null, null, null);
        var wait = new TicketHold(TicketHoldAddressees.Nobody, TicketHoldCases.Prerequisite, null, T) { Names = ["#19694"] };

        _buckets.Held(failed, wait).Bucket.Should().Be(TicketBucket.Blocked);
        _buckets.Held(failed, new TicketHold(TicketHoldAddressees.Nobody, TicketHoldCases.MergeAfter, null, T)).Bucket
            .Should().Be(TicketBucket.Failed, "only a prerequisite wait moves the bucket");
    }

    [Fact]
    public void TakenUpAt_TheNewestTellRefreshOrRetarget_OnThatTicket()
    {
        SeriesAct[] acts =
        [
            new(T, "tell_ticket", "19664", "wrote to #19664", "rebase") { DoneAt = T.AddMinutes(1) },
            new(T.AddMinutes(10), SeriesPullRequestTools.CiTool, "19664", "read the CI of #19664", null),
            new(T.AddMinutes(20), SeriesPullRequestTools.RefreshTool, "19665", "refreshed #19665", "conflict"),
        ];

        TicketSeriesWait.TakenUpAt(acts, "19664").Should().Be(T.AddMinutes(1));
        TicketSeriesWait.TakenUpAt(acts, "19666").Should().BeNull();
    }
}
