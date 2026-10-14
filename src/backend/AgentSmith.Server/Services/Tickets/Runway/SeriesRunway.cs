using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Models;
using static AgentSmith.Server.Services.Tickets.Runway.SeriesHeldReasons;

namespace AgentSmith.Server.Services.Tickets.Runway;

/// <summary>
/// 2026-10-13-8762v: whether a ticket's series goes on by itself. The CHAIN is the attempts back to and including the
/// newest one a person or a trigger started (a cause other than series); its spend is the summed run cost and its
/// elapsed time the summed run time of each attempt, so time held between attempts never counts. A ticket with no
/// cut, or none of whose specs is left, is complete; otherwise the first hold that matches, else start. An attempt
/// continues only on an end <see cref="SeriesBoundaryEnd"/> allows — never a list of its own. A live attempt is
/// judged on the caps and a holding default only. Computed per read, never stored.
/// </summary>
public static class SeriesRunway
{
    public static TicketSeriesBudgetView Judge(SeriesRunwayFacts facts, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(facts);
        var chain = Chain(facts);
        var spent = chain.Sum(r => r.CostTotalUsd);
        var elapsed = chain.Sum(r => Hours(r, now));
        var series = facts.Settings.Series;
        var held = facts.HasCut && facts.SpecsLeft > 0 ? Held(facts, spent, elapsed) : null;
        var next = !facts.HasCut || facts.SpecsLeft == 0 ? Complete : held is null ? Start : Hold;
        return new TicketSeriesBudgetView(spent, series.BudgetUsd, Math.Round(elapsed, 2), series.WallClockHours,
            facts.Attempts.Count - chain.Count + 1, next, held);
    }

    /// <summary>The chain, oldest first: the newest attempts back to and including the newest one not started by the series.</summary>
    public static IReadOnlyList<Run> Chain(SeriesRunwayFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        var chain = new List<Run>();
        foreach (var attempt in facts.Attempts.Reverse())
        {
            chain.Insert(0, attempt);
            if (facts.Causes.GetValueOrDefault(attempt.Id) != AttemptChannels.Series) break;
        }
        return chain;
    }

    private static string? Held(SeriesRunwayFacts facts, decimal spent, double elapsed)
    {
        var series = facts.Settings.Series;
        if (series.BudgetUsd == 0) return Off;
        if (facts.Settings.Problems().Count > 0) return Misconfigured;
        var newest = facts.Newest;
        var live = facts.Queued || newest is null || newest.FinishedAt is null && !RunStatuses.IsWaiting(newest.Status);
        if (!live && Ended(facts, newest!) is { } end) return end;
        if (spent >= series.BudgetUsd) return BudgetUsd;
        if (elapsed >= series.WallClockHours) return WallClock;
        if (facts.PendingHoldingSeries > 0) return DefaultPending;
        if (live) return null;
        return facts.RefusedMark ? Refused : facts.ClosedMark ? Closed : null;
    }

    // How the newest attempt ended, judged on its row: a hand-back, an operator's stop, an end the boundary rule
    // does not continue, or no spec done.
    private static string? Ended(SeriesRunwayFacts facts, Run newest)
    {
        if (newest.Status == RunStatuses.WaitingForInput) return HandedBack;
        var wallTime = newest.Status == SeriesBoundaryEnd.Cancelled && SeriesBoundaryEnd.Ends(newest.Status, newest.CancelReason);
        if (newest.Status == SeriesBoundaryEnd.Cancelled && !(wallTime && facts.Settings.Series.ContinueAfterWallTime))
            return Cancelled;
        if (facts.Watermark is { } stop && stop >= newest.StartedAt) return Cancelled;
        if (!SeriesBoundaryEnd.Ends(newest.Status, newest.CancelReason)) return Incomplete;
        return facts.NewestDoneSpecs ? null : NoProgress;
    }

    // A queued or waiting attempt adds nothing; a resumed one counts from its resume (StartedAt is reset).
    private static double Hours(Run run, DateTimeOffset now) =>
        RunStatuses.IsWaiting(run.Status) ? 0 : Math.Max(0, ((run.FinishedAt ?? now) - run.StartedAt).TotalHours);
}
