namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// 2026-11-17-f9b0d: how the server keeps filed tickets' requirements and landings current, from agentsmith.yml's
/// top-level <c>tickets:</c> block — the bound on showing requires links on a tracker, the window and cap of the
/// dependents-wake recovery, and the cadence and caps of the pull-request re-read. The values the operator ratified
/// on 2026-11-17 are the defaults. Global by design.
/// </summary>
public sealed class TicketsConfig
{
    /// <summary>The seconds one filing waits for its tracker to show every requires link.</summary>
    public int RequiresLinkDeadlineSeconds { get; set; } = 60;

    /// <summary>The hours of landings whose dependents the recovery re-wakes.</summary>
    public int DependentsWakeWindowHours { get; set; } = 24;

    /// <summary>The most dependents one recovery pass reads.</summary>
    public int DependentsWakeCap { get; set; } = 50;

    /// <summary>The seconds between two pull-request re-read passes.</summary>
    public int PrRereadIntervalSeconds { get; set; } = 300;

    /// <summary>The seconds a pull-request record goes unchecked before the re-read names it.</summary>
    public int PrRereadStaleSeconds { get; set; } = 900;

    /// <summary>The most open or unread records one re-read pass names.</summary>
    public int PrRereadOpenCap { get; set; } = 50;

    /// <summary>The most merged records without a landing one re-read pass names.</summary>
    public int PrRereadMergedCap { get; set; } = 50;

    /// <summary>What is wrong with these values, one sentence each; empty when nothing is. Every value must be 1 or
    /// more: a zero interval would spin and a zero cap would read nothing.</summary>
    public IReadOnlyList<string> Problems() =>
        new (string Key, int Value)[]
        {
            ("requires_link_deadline_seconds", RequiresLinkDeadlineSeconds), ("dependents_wake_window_hours", DependentsWakeWindowHours),
            ("dependents_wake_cap", DependentsWakeCap), ("pr_reread_interval_seconds", PrRereadIntervalSeconds),
            ("pr_reread_stale_seconds", PrRereadStaleSeconds), ("pr_reread_open_cap", PrRereadOpenCap),
            ("pr_reread_merged_cap", PrRereadMergedCap),
        }.Where(v => v.Value < 1).Select(v => $"tickets.{v.Key} is {v.Value}; it must be 1 or more").ToList();
}
