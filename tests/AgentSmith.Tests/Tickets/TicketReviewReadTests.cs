using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Tickets;
using FluentAssertions;
using Moq;
using static AgentSmith.Tests.Tickets.TicketStoreHarness;

namespace AgentSmith.Tests.Tickets;

/// <summary>2026-10-13-9a59b: the ticket reads over the pull-request records — buckets, cells, counts, backfill.</summary>
public sealed class TicketReviewReadTests : IDisposable
{
    private readonly TicketStoreHarness _h = new();

    public void Dispose() => _h.Dispose();

    private static string Url(string ticket) => $"https://github.com/o/api/pull/{ticket}";

    private static Run WithPr(Run run, DateTimeOffset? openedAt = null)
    {
        run.PullRequestsJson = RunStoryJson.Serialize(new List<RunPullRequestView>
            { new("api", PullRequestStatuses.Opened, Url(run.TicketId), null, openedAt ?? T) });
        return run;
    }

    private static PrReviewRecord Record(string ticket, string state, DateTimeOffset stateAt, DateTimeOffset? checkedAt = null) => new()
    {
        PrKey = PrKey.Of(Url(ticket)), Url = Url(ticket), Repo = "api", Number = ticket, Project = "alpha", TicketId = ticket,
        RunId = "r", State = state, StateAtTicks = stateAt.UtcTicks, CheckedAtTicks = (checkedAt ?? T.AddHours(3)).UtcTicks,
    };

    private static PrReviewEntryView Standing(string decision, string login, bool trusted, int minutes) =>
        new($"h-{login}", PrReviewEntryKinds.Review, decision, true, login, trusted, false, false, false,
            null, null, null, null, string.Empty, false, T.AddMinutes(minutes));

    [Fact]
    public void TicketReviewState_UntrustedChangesRequested_SaysNoAttempt()
    {
        var record = new PrReviewRecordView("k", Url("1"), "api", "1", "alpha", "1", "r", PrReviewStates.Open, T, T, T.AddMinutes(9),
            [Standing(PrReviewDecisions.Approved, "alice", true, 9), Standing(PrReviewDecisions.ChangesRequested, "mallory", false, 5)]);

        var view = new TicketReviewState().Of(record, null);

        view.Decision.Should().Be(TicketReviewDecisions.ChangesRequestedUntrusted);
        view.By.Should().Be("mallory");
        view.Note.Should().Contain("no attempt was started");
    }

    [Fact]
    public async Task TicketBuckets_MergedPr_IsDone_LiveAttemptBeatsWaitingOnReview()
    {
        await _h.AddAsync(WithPr(Run("r-42", "42")), WithPr(Run("r-43", "43", status: "running")), WithPr(Run("r-44", "44")),
            Step("r-42"), Step("r-43"), Step("r-44"),
            Record("42", PrReviewStates.Merged, T.AddHours(1)), Record("43", PrReviewStates.Open, T), Record("44", PrReviewStates.Open, T));

        var tickets = (await _h.List.ReadAsync(new TicketListRequest("alpha", null, null, null), default)).Tickets.ToDictionary(t => t.TicketId);

        tickets["42"].Bucket.Should().Be(TicketBucket.Done);
        tickets["42"].Since.Should().Be(T.AddHours(1));
        tickets["43"].Bucket.Should().Be(TicketBucket.Working, "a live attempt outranks a pull request waiting on review");
        tickets["44"].Bucket.Should().Be(TicketBucket.WaitingOnReview);
        tickets["44"].PullRequests.Single().Should().Match<TicketPullRequestView>(p =>
            p.Number == "44" && p.State == PrReviewStates.Open && p.Decision == TicketReviewDecisions.None && p.Repo == "api");
    }

    [Fact]
    public async Task TicketBuckets_AttemptAfterMerge_IsNotDone()
    {
        await _h.AddAsync(WithPr(Run("r-42", "42", status: "failed")), Step("r-42"), Record("42", PrReviewStates.Merged, T.AddHours(-1)));

        var ticket = (await _h.List.ReadAsync(new TicketListRequest("alpha", null, null, null), default)).Tickets.Single();

        ticket.Bucket.Should().Be(TicketBucket.Idle, "an attempt started after the merge is that attempt's ticket");
    }

    [Fact]
    public async Task TicketReviewState_NoEntries_LastActivityIsOpenedAt()
    {
        await _h.AddAsync(WithPr(Run("r-42", "42"), openedAt: T.AddMinutes(5)), Step("r-42"), Record("42", PrReviewStates.Open, T.AddMinutes(10)));

        var detail = await _h.Detail.ReadAsync("alpha", "42", default);

        detail!.Summary.PullRequests.Single().LastActivityAt.Should().Be(T.AddMinutes(5));
    }

    [Fact]
    public async Task TicketCounts_OpenPullRequestsAndDoneThisMonthWithAttempts()
    {
        await _h.AddAsync(Run("r-41a", "41"), WithPr(Run("r-41b", "41")), WithPr(Run("r-42", "42")), Step("r-41a"), Step("r-41b"), Step("r-42"),
            Record("41", PrReviewStates.Merged, T.AddHours(2)), Record("42", PrReviewStates.Open, T));

        var counts = (await _h.List.ReadAsync(new TicketListRequest("alpha", null, null, null), default)).Counts;

        counts.OpenPullRequests.Should().Be(1);
        counts.WaitingOnReview.Should().Be(1);
        counts.Done.Should().Be(1);
        counts.DoneThisMonth.Should().Be(new TicketDoneCount(1, 2));
        counts.LastDone.Should().Be(new TicketLastDone("alpha", "41", T.AddHours(2)));
    }

    [Fact]
    public async Task TicketList_PrWithoutRecord_EnqueuesRefreshOnce()
    {
        await _h.AddAsync(WithPr(Run("r-42", "42")), WithPr(Run("r-43", "43")), Step("r-42"), Step("r-43"), Record("43", PrReviewStates.Open, T));

        await _h.List.ReadAsync(new TicketListRequest("alpha", null, null, null), default);

        _h.Nudges.Verify(n => n.EnqueueAsync(It.Is<ReworkNudgeRequest>(r =>
            r.Project == "alpha" && r.TicketId == "42" && r.Origin == ReworkNudgeOrigin.Refresh && r.IsLate), default), Times.Once);
        _h.Nudges.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task TicketDetail_StaleRecord_EnqueuesOneRefresh()
    {
        await _h.AddAsync(WithPr(Run("r-42", "42")), WithPr(Run("r-43", "43")), Step("r-42"), Step("r-43"),
            Record("42", PrReviewStates.Open, T, checkedAt: T.AddHours(3).AddMinutes(-10)),
            Record("43", PrReviewStates.Open, T, checkedAt: T.AddHours(3).AddMinutes(-1)));

        await _h.Detail.ReadAsync("alpha", "42", default);
        await _h.Detail.ReadAsync("alpha", "43", default);

        _h.Nudges.Verify(n => n.EnqueueAsync(It.Is<ReworkNudgeRequest>(r => r.TicketId == "42" && r.Origin == ReworkNudgeOrigin.Refresh), default), Times.Once);
        _h.Nudges.VerifyNoOtherCalls();
    }
}
