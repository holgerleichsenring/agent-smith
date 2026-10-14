namespace AgentSmith.Contracts.Runs;

/// <summary>
/// 2026-10-13-8762g: the ends an attempt can stop at a boundary with — a run that ended success, or one
/// the watchdog cancelled at the wall-time ceiling. A shortfall delivered what it verified and a failure
/// or an operator's cancel is not a boundary. The one rule every reader of the boundary asks.
/// </summary>
public static class SeriesBoundaryEnd
{
    public const string Cancelled = "cancelled";

    private static readonly string[] WatchdogReasons = ["watchdog", "watchdog-wall-time"];

    public static bool Ends(string? status, string? cancelReason) =>
        status == RunStatuses.Success
        || (status == Cancelled && cancelReason is not null && WatchdogReasons.Contains(cancelReason));
}
