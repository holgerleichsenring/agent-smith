using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Series;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Series;

/// <summary>2026-11-25-96aea: a standalone ticket is a series of one — opened by its code run unless a series holds it, led
/// by its master, ended once its ticket landed with no series check, and, where a person decides the merge, asking it
/// through the ticket's merge hold instead of a master turn. A code run merges nothing.</summary>
public sealed class SeriesOfOneTests
{
    private static readonly ResolvedProject Project = new() { Name = "sample", Review = ReviewMode.End };
    private readonly InMemorySeriesRunStore _store = new();
    private readonly SettableClock _clock = new() { Now = SeriesRecords.T };

    private SeriesOfOne One(string? conversation = null) => new(_store, new SeriesMembership(_store, _clock),
        Mock.Of<ISeriesConversation>(c => c.TicketConversationAsync(It.IsAny<ResolvedProject>(), It.IsAny<string>(), It.IsAny<CancellationToken>())
            == Task.FromResult(conversation)), _clock);

    private static PipelineContext Fetched(string id, string title)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.Ticket, new Ticket(new TicketId(id), title, string.Empty, null, "open", "github"));
        return pipeline;
    }

    [Fact]
    public async Task OpenTicketSeries_ATicketNoSeriesHolds_OpensASeriesOfOne_ASecondRunJoinsIt()
    {
        var handler = new OpenTicketSeriesHandler(One("conv-7"));

        (await handler.ExecuteAsync(new OpenTicketSeriesContext(Fetched("7", "Fix the login"), Project), default)).IsSuccess.Should().BeTrue();
        await handler.ExecuteAsync(new OpenTicketSeriesContext(Fetched("7", "Fix the login"), Project), default);

        var one = _store.Records.Values.Should().ContainSingle("the second run finds the ticket held").Subject;
        (one.IsOne, one.Tickets[0].Title, one.Conversation, one.Principal, one.MayStartRuns, one.IntegrationBranch, one.Review)
            .Should().Be((true, "Fix the login", "conv-7", Project.ActorName, true, (string?)null, ReviewMode.Each));
    }

    [Fact]
    public async Task OpenAsync_AStoppedSeries_IsNotReopened_AndARacingSecondOpenerOpensNothing()
    {
        await _store.OpenAsync(SeriesRecords.Running("sr-0") with { Tickets = [SeriesRecords.Ticket("7")], State = SeriesRunStates.Stopped }, default);
        (await One().OpenAsync(Project, "7", "t", default)).Should().BeNull("a stop is a decision");

        await _store.OpenAsync(SeriesRecords.Running("sr-1") with { Tickets = [SeriesRecords.Ticket("8")], State = SeriesRunStates.Done }, default);
        var id = SeriesRunIds.OneOf(Project.Name, "8", "sr-1");
        id.Should().Be(SeriesRunIds.OneOf(Project.Name, "8", "sr-1"), "two openers mint the same id, so the store's unique id takes one");
        (await One().OpenAsync(Project, "8", "t", default)).Should().Be(id);
    }

    [Fact]
    public async Task OpenTicketSeries_WithoutAStoreOrATicket_OpensNothing()
    {
        (await new OpenTicketSeriesHandler().ExecuteAsync(new OpenTicketSeriesContext(Fetched("7", "t"), Project), default))
            .Message.Should().Contain("No series store");
        (await new OpenTicketSeriesHandler(One()).ExecuteAsync(new OpenTicketSeriesContext(new PipelineContext(), Project), default))
            .Message.Should().Contain("No tracker ticket");
        _store.Records.Should().BeEmpty();
    }

    [Theory]
    [InlineData(true, true, 0, false, true)]
    [InlineData(true, false, 0, false, false)]
    [InlineData(true, true, 1, false, false)]
    [InlineData(false, true, 0, false, false)]
    public void OneIsDone_WhenItsTicketLanded_NoRunWorks_AndNoPullRequestWaits(bool landed, bool finished, int waiting, bool working, bool done)
    {
        var fact = SeriesRecords.Fact("7", working ? RunStatuses.Running : RunStatuses.Success, finished, landed ? SeriesRecords.T : null)
            with { PullRequests = [.. Enumerable.Repeat(new SeriesRunPullRequestView("api", "1", "https://host/pr/1", PrReviewStates.Open, null, null, null, null), waiting)] };

        var record = SeriesRecords.Running() with { OneWaited = true };
        SeriesProgress.OneIsDone(record, [fact]).Should().Be(done);
        SeriesProgress.OneIsDone(record, [fact, fact]).Should().BeFalse("a series of two runs its series check");
        SeriesProgress.OneIsDone(record with { OneWaited = false }, [fact]).Should().BeFalse(
            "a landing it never saw a pull request of was its ticket's earlier spec");
    }

    [Fact]
    public async Task ASeriesOfOne_EndsDoneOnItsLanding_WithoutASeriesCheck()
    {
        var launch = new Mock<ISeriesRunLaunch>();
        var record = SeriesRecords.Running() with { Tickets = [SeriesRecords.Ticket("7")] };
        await _store.OpenAsync(record, default);
        var open = new SeriesRunPullRequestView("api", "1", "https://host/pr/1", PrReviewStates.Open, null, null, null, null);
        var waiting = new SeriesBeat(record, [SeriesRecords.Fact("7", RunStatuses.Success, landed: SeriesRecords.T) with { PullRequests = [open] }], SeriesRecords.T);
        await new SeriesProgress(_store, launch.Object).MoveAsync(waiting, default);
        _store.Records["sr-1"].State.Should().Be(SeriesRunStates.Running, "an old landing, a pull request of its own still waits");

        var beat = new SeriesBeat(_store.Records["sr-1"], [SeriesRecords.Fact("7", RunStatuses.Success, landed: SeriesRecords.T)], SeriesRecords.T);
        await new SeriesProgress(_store, launch.Object).MoveAsync(beat, default);

        _store.Records["sr-1"].State.Should().Be(SeriesRunStates.Done);
        launch.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(ReviewMode.Each, 1, false)]
    [InlineData(ReviewMode.None, 1, true)]
    [InlineData(ReviewMode.Each, 2, true)]
    public void AGreenCi_OnASeriesOfOneWhoseMergeAPersonDecides_IsNoTurn(ReviewMode review, int tickets, bool wakes)
    {
        var record = SeriesRecords.Running() with { Review = review, Tickets = [.. SeriesRecords.Running().Tickets.Take(tickets)] };
        var green = new SeriesWake(SeriesWakeKinds.Ci, "the CI is green") { Verdict = CiStates.Green, TicketId = "43", At = SeriesRecords.T };

        SeriesBeatDue.Waking(record, green).Should().Be(wakes);
        SeriesBeatDue.Waking(record, green with { Verdict = CiStates.None }).Should().Be(wakes, "2026-11-27-e451: no CI merges as green does");
        SeriesBeatDue.Waking(record, green with { Verdict = CiStates.Red }).Should().BeTrue("a red CI is the master's decision");
    }

    [Fact]
    public void AMergeQuestion_OffersBothButtons_LinksEveryPullRequest_AndTakesNoDefault()
    {
        var fact = SeriesRecords.Fact("43", RunStatuses.Success) with
        {
            PullRequests = [new SeriesRunPullRequestView("api", "12", "https://host/o/api/pull/12", PrReviewStates.Open, CiStates.Green, null, null, "main")],
        };
        var beat = new SeriesBeat(SeriesRecords.Running(), [fact], SeriesRecords.T);

        var asked = SeriesMergeQuestion.Of(beat, "SCRUM-43 is green. Merge it?", "SCRUM-43");

        asked.Choices.Should().Equal(SeriesAskTool.MergeIt, SeriesAskTool.LookFirst);
        (asked.NoDefault, asked.DeadlineAt, asked.RecommendedIndex).Should().Be((true, (DateTimeOffset?)null, (int?)null));
        asked.Text.Should().Contain("[SCRUM-43 in api (#12)](https://host/o/api/pull/12)");
    }

    [Fact]
    public async Task AComment_OnASeriesOfOne_KeepsItsTicketsRework_OnAFiledSeries_ReachesTheMaster()
    {
        await _store.OpenAsync(SeriesRecords.Running("sr-1") with { Tickets = [SeriesRecords.Ticket("7")] }, default);
        await _store.OpenAsync(SeriesRecords.Running("sr-2"), default);

        ISeriesRunStore store = _store;
        (await store.LeadsCommentsAsync("sample", "7", default)).Should().BeFalse();
        (await store.LeadsCommentsAsync("sample", "43", default)).Should().BeTrue();
    }

    [Fact]
    public async Task ACodeRun_MergesNothing_TheSeriesMasterDoes()
    {
        var handler = new CompletePullRequestsHandler(null!);

        (await handler.ExecuteAsync(new CompletePullRequestsContext(ReviewMode.None, BranchProtection.Respect, null, [], new PipelineContext()), default))
            .Message.Should().Be(CompletePullRequestsHandler.LeftToTheMaster);
    }
}
