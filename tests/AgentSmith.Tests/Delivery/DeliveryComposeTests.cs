using AgentSmith.Application.Services;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Delivery;
using FluentAssertions;

namespace AgentSmith.Tests.Delivery;

/// <summary>
/// 2026-10-25-0099b: how the delivery overview counts — an attempt's outcome and cause, a ticket's merge inside its
/// span, and the figures of a span, with nothing divided by zero.
/// </summary>
public sealed class DeliveryComposeTests
{
    private static readonly DateTimeOffset T = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
    private readonly DeliveryAttemptOutcome _outcome = new();

    [Theory]
    [InlineData("success", null, true, "delivered", "success")]
    [InlineData("shortfall", null, true, "shortfall", "shortfall")]
    [InlineData("failed", "criteria-unmet", true, "shortfall", "criteria-unmet")]
    [InlineData("cancelled", "superseded", true, "cancelled", "superseded")]
    [InlineData("cancelled", null, true, "cancelled", "cancelled")]
    [InlineData("failed", "preflight", true, "failed", "preflight")]
    [InlineData("failed", null, true, "failed", "failed")]
    [InlineData("running", null, false, "running", null)]
    [InlineData("queued", null, false, "queued", null)]
    [InlineData("waiting_for_input", null, true, "waiting", null)]
    public void DeliveryAttemptOutcome_StatusAndEndKind_MapToOutcomeAndCause(
        string status, string? endKind, bool finished, string outcome, string? cause)
    {
        var attempt = _outcome.Of(Row("r1", status, endKind, finished), [], [], T.AddHours(1));

        attempt.Outcome.Should().Be(outcome);
        attempt.Cause.Should().Be(cause);
    }

    [Fact]
    public void DeliveryAttemptOutcome_ReworkAndSpecs_ReadFromTheRow()
    {
        var specs = new[] { new DeliverySpecRow("r1", 2, "second", "failed"), new DeliverySpecRow("r1", 1, "first", "done") };

        var attempt = _outcome.Of(Row("r1", "success", cause: "ticket_comment"), specs, ["api"], T);

        attempt.Rework.Should().BeTrue();
        _outcome.Of(Row("r2", "success", cause: "retry"), [], [], T).Rework.Should().BeFalse();
        (attempt.SpecsDone, attempt.SpecsTotal).Should().Be((1, 2));
        attempt.SpecTitles.Should().Equal("first", "second");
    }

    [Fact]
    public void DeliveryFigures_AgentSeconds_SkipQueuedAndWaiting()
    {
        var now = T.AddMinutes(30);

        _outcome.Of(Row("r1", "running", finished: false), [], [], now).Seconds.Should().Be(1800);
        _outcome.Of(Row("r2", "queued", finished: false), [], [], now).Seconds.Should().Be(0);
        _outcome.Of(Row("r3", "waiting_for_input", finished: true), [], [], now).Seconds.Should().Be(0);
        _outcome.Of(Row("r4", "success") with { DurationSeconds = 42 }, [], [], now).Seconds.Should().Be(42);
        _outcome.Of(Row("r5", "failed"), [], [], now).Seconds.Should().Be(600, "a finished run without a duration counts start to finish");
    }

    [Fact]
    public void DeliveryTicketComposer_MergeIsTheEarliestInsideTheSpanAfterTheFirstAttempt()
    {
        var span = Span(T, T.AddDays(1));
        var attempts = new[] { Attempt(Id(T.AddHours(2)), "alpha", "T-1"), Attempt(Id(T.AddHours(1)), "alpha", "T-1") };
        var records = new[]
        {
            Record("alpha", "T-1", PrReviewStates.Merged, T.AddMinutes(30)),
            Record("alpha", "T-1", PrReviewStates.Merged, T.AddHours(5)),
            Record("alpha", "T-1", PrReviewStates.Merged, T.AddHours(3)),
            Record("alpha", "T-1", PrReviewStates.Merged, T.AddDays(2)),
            Record("alpha", "T-1", PrReviewStates.Closed, T.AddHours(2)),
        }.ToLookup(r => (r.Project, r.TicketId));

        var ticket = new DeliveryTicketComposer().Compose(attempts, span, records).Should().ContainSingle().Subject;

        ticket.Attempts.Select(a => a.Row.RunId).Should().Equal(Id(T.AddHours(1)), Id(T.AddHours(2)));
        ticket.MergedAt.Should().Be(T.AddHours(3), "before the first attempt and after the span do not count");
    }

    [Fact]
    public void DeliveryTicketComposer_AttemptOutsideTheSpan_IsNotItsTicket()
    {
        var records = Array.Empty<PrReviewRecordView>().ToLookup(r => (r.Project, r.TicketId));

        new DeliveryTicketComposer().Compose([Attempt(Id(T.AddDays(-1)), "alpha", "T-1")], Span(T, T.AddDays(1)), records)
            .Should().BeEmpty();
    }

    [Fact]
    public void DeliveryFigures_RatesMedianReworkAndCost_NullOnEmptyDenominator()
    {
        var merged = new DeliveryTicket("alpha", "T-1", [Attempt(Id(T), "alpha", "T-1", cost: 4m), Attempt(Id(T.AddHours(1)), "alpha", "T-1", cost: 2m, rework: true)], T.AddHours(4));
        var open = new DeliveryTicket("alpha", "T-2", [Attempt(Id(T), "alpha", "T-2", cost: 2m)], null);
        var calculator = new DeliveryFiguresCalculator();

        var figures = calculator.Of([merged, open]);
        var none = calculator.Of([]);

        (figures.TicketsWorked, figures.Attempts, figures.Merged).Should().Be((2, 3, 1));
        figures.MergeRate.Should().Be(0.5);
        figures.MedianTimeToMergeSeconds.Should().Be(4 * 3600);
        figures.ReworkPerTicket.Should().Be(0.5);
        (figures.SpendUsd, figures.CostPerMergedUsd).Should().Be((8m, 8m));
        (none.MergeRate, none.MedianTimeToMergeSeconds, none.ReworkPerTicket, none.CostPerMergedUsd).Should().Be((null, null, null, null));
    }

    [Fact]
    public void DeliveryWindowFactory_EachPeriod_HasItsSpanSlotsAndPrevious()
    {
        var factory = new DeliveryWindowFactory(new StoppedClock(T.AddMilliseconds(400)));

        var day = factory.Create("day")!;
        var week = factory.Create(null)!;
        var month = factory.Create("Month")!;

        day.Current.End.Should().Be(T.AddSeconds(1), "the window ends at the next whole second");
        (day.Current.End - day.Current.Start).Should().Be(TimeSpan.FromDays(1));
        day.Slots.Should().HaveCount(24);
        day.Previous.End.Should().Be(day.Current.Start);
        (week.Period, week.Slots.Count, month.Period, month.Slots.Count).Should().Be(("week", 7, "month", 30));
        month.Slots[0].Start.Should().Be(month.Current.Start);
        month.Slots[^1].ToKey.Should().Be(month.Current.ToKey);
    }

    [Fact]
    public void DeliveryWindowFactory_UnknownPeriod_IsNull() =>
        new DeliveryWindowFactory(TimeProvider.System).Create("year").Should().BeNull();

    [Fact]
    public void RunIdGenerator_TimestampOf_IsTheIdPrefix()
    {
        var id = RunIdGenerator.Generate(T);

        RunIdGenerator.TimestampOf(T).Should().Be("2026-10-20T12-00-00");
        id.Should().StartWith(RunIdGenerator.TimestampOf(T));
        string.CompareOrdinal(id, RunIdGenerator.TimestampOf(T.AddSeconds(1))).Should().BeNegative();
    }

    private static string Id(DateTimeOffset at) => RunIdGenerator.TimestampOf(at) + "-0001";

    private static DeliverySpan Span(DateTimeOffset from, DateTimeOffset to) =>
        new(from, to, RunIdGenerator.TimestampOf(from), RunIdGenerator.TimestampOf(to));

    private static DeliveryAttemptRow Row(
        string id, string status, string? endKind = null, bool finished = true, string? cause = null) =>
        new(id, "alpha", "T-1", "title", status, endKind, T, finished ? T.AddMinutes(10) : null, null, 1m, cause);

    private static DeliveryAttempt Attempt(string id, string project, string ticket, decimal cost = 1m, bool rework = false) =>
        new(new DeliveryAttemptRow(id, project, ticket, null, "success", null, ParseId(id), null, 60, cost, null),
            "delivered", "success", rework, 60, 0, 0, [], []);

    private static DateTimeOffset ParseId(string id) => DateTimeOffset.ParseExact(
        id[..19], "yyyy-MM-ddTHH-mm-ss", System.Globalization.CultureInfo.InvariantCulture,
        System.Globalization.DateTimeStyles.AssumeUniversal);

    private static PrReviewRecordView Record(string project, string ticket, string state, DateTimeOffset at) =>
        new($"k-{at.Ticks}", "https://example.test/pr/1", "repo", "1", project, ticket, "r", state, at, at, null, []);

    private sealed class StoppedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
