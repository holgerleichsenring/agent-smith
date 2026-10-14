using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Services.Tickets.Runway;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using static AgentSmith.Server.Services.Tickets.Runway.SeriesHeldReasons;

namespace AgentSmith.Tests.Tickets.Runway;

/// <summary>2026-10-13-8762v: the runway judged on its facts — complete, start, or the first hold that matches.</summary>
public sealed class SeriesRunwayTests
{
    private static readonly DateTimeOffset T = new(2026, 10, 14, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = T.AddHours(30);

    private static Run R(int n, string status = RunStatuses.Success, decimal cost = 10m, double hours = 1, string? reason = null) => new()
    {
        Id = $"run-{n:00}", Pipeline = "code", Status = status, StartedAt = T.AddHours(n * 2), CostTotalUsd = cost,
        CancelReason = reason, FinishedAt = status is "running" || RunStatuses.IsWaiting(status) ? null : T.AddHours(n * 2 + hours),
    };

    private static SeriesRunwayFacts Facts(IReadOnlyList<Run> attempts, SeriesConfig? series = null, params string?[] causes) => new(
        attempts, attempts.Select((a, i) => (a.Id, Cause: i < causes.Length ? causes[i] : AttemptChannels.Series))
            .ToDictionary(a => a.Id, a => a.Cause, StringComparer.Ordinal),
        attempts.LastOrDefault(), Queued: false, HasCut: true, SpecsLeft: 4, NewestDoneSpecs: true, PendingHoldingSeries: 0,
        Watermark: null, RefusedMark: false, ClosedMark: false, SeriesSettingsDoubles.Settings(series));

    private static SeriesRunwayFacts One(Run run, SeriesConfig? series = null) => Facts([run], series, AttemptChannels.Pickup);

    private static (string Next, string? Held) Judge(SeriesRunwayFacts facts)
    {
        var view = SeriesRunway.Judge(facts, Now);
        return (view.Next, view.HeldBecause);
    }

    [Fact]
    public void SeriesRunway_FullChunkWithTail_IsStart() => Judge(One(R(1))).Should().Be((Start, null));

    [Fact]
    public void SeriesRunway_NothingLeft_IsComplete() => Judge(One(R(1)) with { SpecsLeft = 0 }).Should().Be((Complete, null));

    [Fact]
    public void SeriesRunway_NoCut_IsComplete() =>
        Judge(One(R(1, RunStatuses.Failed)) with { HasCut = false, SpecsLeft = 0 }).Should().Be((Complete, null));

    [Fact]
    public void SeriesRunway_HandedBack_IsHeld() => Judge(One(R(1, RunStatuses.WaitingForInput))).Should().Be((Hold, HandedBack));

    [Fact]
    public void SeriesRunway_FailedOrShortfall_IsHeldIncomplete()
    {
        Judge(One(R(1, RunStatuses.Failed))).Should().Be((Hold, Incomplete));
        Judge(One(R(1, RunStatuses.Shortfall))).Should().Be((Hold, Incomplete), "a cost-cap stop delivers a shortfall");
    }

    [Fact]
    public void SeriesRunway_OperatorCancel_IsHeldCancelled() =>
        Judge(One(R(1, "cancelled", reason: "operator"))).Should().Be((Hold, Cancelled));

    [Fact]
    public void SeriesRunway_QueuedAttemptCancelledByOperator_IsHeldCancelled()
    {
        var dropped = R(2, "cancelled", reason: null);
        var facts = One(R(1)) with { Newest = dropped };

        Judge(facts).Should().Be((Hold, Cancelled), "a queued cancel or a queue drop is newest though it is no attempt");
    }

    [Fact]
    public void SeriesRunway_NewestAttemptDeleted_IsHeldByTheWatermark()
    {
        var older = R(1);

        Judge(One(older) with { Watermark = older.StartedAt.AddHours(5) }).Should().Be((Hold, Cancelled));
        Judge(One(older) with { Watermark = older.StartedAt.AddHours(-1) }).Should().Be((Start, null));
    }

    [Fact]
    public void SeriesRunway_WallTimeCancelWithProgress_IsStart_UnlessContinueAfterWallTimeIsOff()
    {
        Judge(One(R(1, "cancelled", reason: "watchdog"))).Should().Be((Start, null));
        Judge(One(R(1, "cancelled", reason: "watchdog-wall-time"))).Should().Be((Start, null));
        Judge(One(R(1, "cancelled", reason: "watchdog"), new SeriesConfig { ContinueAfterWallTime = false }))
            .Should().Be((Hold, Cancelled));
        Judge(One(R(1, "cancelled", reason: "watchdog")) with { NewestDoneSpecs = false }).Should().Be((Hold, NoProgress));
    }

    [Fact]
    public void SeriesRunway_NoDoneSpec_IsHeldNoProgress() =>
        Judge(One(R(1)) with { NewestDoneSpecs = false }).Should().Be((Hold, NoProgress));

    [Fact]
    public void SeriesRunway_SpendAtCap_IsHeldBudget()
    {
        var view = SeriesRunway.Judge(Facts([R(1, cost: 100m), R(2, cost: 50m)], null, AttemptChannels.Pickup), Now);

        (view.Next, view.HeldBecause).Should().Be((Hold, BudgetUsd));
        (view.SpentUsd, view.CapUsd).Should().Be((150m, 150m));
    }

    [Fact]
    public void SeriesRunway_ElapsedAtCap_IsHeldWallClock()
    {
        var view = SeriesRunway.Judge(Facts([R(1, hours: 7), R(2, hours: 5)], null, AttemptChannels.Pickup), Now);

        (view.Next, view.HeldBecause).Should().Be((Hold, WallClock));
        view.ElapsedHours.Should().Be(12);
    }

    [Fact]
    public void SeriesRunway_RatifyAfterLongHold_Starts()
    {
        var held = One(R(1, hours: 2)) with { PendingHoldingSeries = 1 };
        Judge(held).Should().Be((Hold, DefaultPending));

        var view = SeriesRunway.Judge(held with { PendingHoldingSeries = 0 }, Now.AddDays(3));

        (view.Next, view.HeldBecause).Should().Be((Start, null));
        view.ElapsedHours.Should().Be(2, "the days the default waited are no run time");
    }

    [Fact]
    public void SeriesRunway_PendingHoldsSeriesDefault_IsHeld() =>
        Judge(One(R(1)) with { PendingHoldingSeries = 2 }).Should().Be((Hold, DefaultPending));

    [Fact]
    public void SeriesRunway_ChainStartsAtTheNewestNonSeriesAttempt()
    {
        var facts = Facts([R(1, cost: 90m), R(2, cost: 20m), R(3, cost: 20m), R(4, cost: 20m)], null,
            AttemptChannels.Requested, AttemptChannels.TicketComment, AttemptChannels.Series, AttemptChannels.Series);

        var view = SeriesRunway.Judge(facts, Now);

        view.ChainFrom.Should().Be(2, "a person's comment began the chain; the attempt before it is spent elsewhere");
        view.SpentUsd.Should().Be(60m);
        SeriesRunway.Chain(facts).Select(r => r.Id).Should().Equal("run-02", "run-03", "run-04");
        view.Next.Should().Be(Start);
    }

    [Fact]
    public void SeriesRunway_LiveAttempt_JudgedOnBudgetAndHoldsOnly()
    {
        var running = R(1, "running");
        running.StartedAt = Now.AddHours(-1);
        var live = One(running) with { NewestDoneSpecs = false, RefusedMark = true };

        Judge(live).Should().Be((Start, null), "a live attempt's own end is not known yet");
        Judge(live with { PendingHoldingSeries = 1 }).Should().Be((Hold, DefaultPending));
        running.CostTotalUsd = 200m;
        Judge(live).Should().Be((Hold, BudgetUsd));
        Judge(One(R(1)) with { Queued = true, NewestDoneSpecs = false }).Should().Be((Start, null), "a queued reservation is live");
    }

    [Fact]
    public void SeriesRunway_BudgetZero_IsHeldOff() =>
        Judge(One(R(1), new SeriesConfig { BudgetUsd = 0 })).Should().Be((Hold, Off));

    [Fact]
    public void SeriesRunway_NegativeBudget_IsHeldMisconfigured()
    {
        Judge(One(R(1), new SeriesConfig { BudgetUsd = -1 })).Should().Be((Hold, Misconfigured));
        Judge(One(R(1), new SeriesConfig { WallClockHours = -2 })).Should().Be((Hold, Misconfigured));
    }

    [Fact]
    public void SeriesRunway_RefusedOrClosedMark_IsHeldSo()
    {
        Judge(One(R(1)) with { RefusedMark = true }).Should().Be((Hold, Refused));
        Judge(One(R(1)) with { ClosedMark = true }).Should().Be((Hold, Closed));
    }
}
