using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Models;
using FluentAssertions;
using static AgentSmith.Tests.Tickets.TicketStoreHarness;

namespace AgentSmith.Tests.Tickets;

/// <summary>Done means a pull request landed: every record ended and at least one merged; the summary says how they ended.</summary>
public sealed class TicketLandingTests : IDisposable
{
    private readonly TicketStoreHarness _h = new();

    public void Dispose() => _h.Dispose();

    private static string Url(string number) => $"https://github.com/o/api/pull/{number}";

    private static PrReviewRecord Record(string number, string state, string ticket = "42", int hours = 1) => new()
    {
        PrKey = PrKey.Of(Url(number)), Url = Url(number), Repo = "api", Number = number, Project = "alpha", TicketId = ticket,
        RunId = "r", State = state, StateAtTicks = T.AddHours(hours).UtcTicks, CheckedAtTicks = T.AddHours(3).UtcTicks,
    };

    private async Task<TicketSummaryView> RowAsync() =>
        (await _h.List.ReadAsync(new TicketListRequest(ProjectScope.Single("alpha"), null, null, null), default)).Tickets.Single();

    [Fact]
    public async Task TicketBuckets_AllPrsClosedUnmerged_IsPrClosedWithLandingUnmerged()
    {
        await _h.AddAsync(Run("r-1"), Step("r-1"),
            Record("1", PrReviewStates.Closed), Record("2", PrReviewStates.Closed), Record("3", PrReviewStates.Closed));

        var row = await RowAsync();

        (row.Bucket, row.Landing, row.LastOutcome).Should().Be((TicketBucket.PrClosed, TicketLanding.Unmerged, "success"));
    }

    [Fact]
    public async Task TicketBuckets_OneMergedOneClosed_IsDoneWithLandingMerged()
    {
        await _h.AddAsync(Run("r-1"), Step("r-1"), Record("1", PrReviewStates.Merged, hours: 2), Record("2", PrReviewStates.Closed));

        var row = await RowAsync();
        var detail = await _h.Detail.ReadAsync("alpha", "42", default);

        (row.Bucket, row.Landing, row.Since).Should().Be((TicketBucket.Done, TicketLanding.Merged, (DateTimeOffset?)T.AddHours(2)));
        (detail!.Summary.Bucket, detail.Summary.Landing).Should().Be((TicketBucket.Done, TicketLanding.Merged));
    }

    [Fact]
    public async Task TicketBuckets_MergedPrFailedAttempts_IsDone()
    {
        await _h.AddAsync(Run("r-1", status: "failed"), Run("r-2", status: "failed"), Step("r-1"), Step("r-2"),
            Record("1", PrReviewStates.Merged), Record("2", PrReviewStates.Merged));

        var row = await RowAsync();

        (row.Bucket, row.Landing).Should().Be((TicketBucket.Done, TicketLanding.Merged), "a person merged what the attempts opened");
    }

    [Fact]
    public async Task TicketBuckets_OnePrOpen_LandingNull()
    {
        await _h.AddAsync(Run("r-1"), Step("r-1"), Record("1", PrReviewStates.Merged), Record("2", PrReviewStates.Open));
        (await RowAsync()).Should().Match<TicketSummaryView>(r => r.Bucket == TicketBucket.WaitingOnReview && r.Landing == null);
    }

    [Fact]
    public async Task TicketBuckets_WorkingTicket_CarriesLanding()
    {
        await _h.AddAsync(Run("r-1", status: "running"), Step("r-1"), Record("1", PrReviewStates.Closed));
        (await RowAsync()).Should().Match<TicketSummaryView>(r => r.Bucket == TicketBucket.Working && r.Landing == TicketLanding.Unmerged);
    }

    [Fact]
    public async Task TicketBuckets_MergedAfterFailedAttempt_StuckObserved_IsDone()
    {
        var run = Run("r-1", status: "failed");
        run.ActsReadAt = T;
        await _h.AddAsync(run, Step("r-1"), Record("1", PrReviewStates.Merged),
            new TicketBetweenAttemptsCheck { RunId = "r-1", CheckedAt = T.AddHours(3), FingerprintUnchanged = true, ActsJson = "[]" });

        var row = await RowAsync();

        (row.UnchangedSinceLastAttempt, row.Bucket, row.Landing).Should().Be(((bool?)true, TicketBucket.Done, TicketLanding.Merged));
    }

    [Theory]
    [InlineData("")]
    [InlineData("merged,closed")]
    [InlineData("closed,closed")]
    [InlineData("merged,open")]
    [InlineData("merged,unread")]
    [InlineData("merged,unsupported")]
    public void TicketLanding_OfStates_AgreesWithOfOverViews(string joined)
    {
        var states = joined.Split(',', StringSplitOptions.RemoveEmptyEntries);
        var views = states.Select((s, i) => new TicketPullRequestView($"{i}", Url($"{i}"), "api", s, "", null, null, null, null)).ToList();

        TicketLanding.OfStates(states).Should().Be(TicketLanding.Of(views));
    }
}
