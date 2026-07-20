namespace AgentSmith.Server.Services.Lifecycle;

/// <summary>
/// p0355: maps the TYPED cancel reason carried on the run row
/// (<c>Run.CancelReason</c>) to the operator-facing run summary and tracker-ticket
/// comment. The enforced-cancel path used to hardcode "Cancelled by operator" for
/// every reason, so a stale-lease REAP (the owning replica went away) read as an
/// operator action. Naming the real reason lets the UI and the ticket tell a reap,
/// a wall-time kill, and a budget stop apart from an actual operator cancel.
/// </summary>
internal static class CancelReasonNarrator
{
    public static string Summary(string? reason) => reason switch
    {
        "stale-lease-reaped" =>
            "Reaped — the owning replica went away (stale lease); cancelled after the grace period.",
        "watchdog-wall-time" =>
            "Cancelled — the run exceeded its wall-time budget (orchestrator.max_run_wall_time_seconds).",
        "budget" =>
            "Cancelled — the run exceeded its cost/capacity budget.",
        "crashed" =>
            "Cancelled — the run crashed and could not continue.",
        "sandbox-vanished" =>
            "Cancelled — the sandbox container exited mid-run (most often an out-of-memory kill).",
        "operator" or null or "" =>
            "Cancelled by operator — enforced after the grace period.",
        _ => $"Cancelled ({reason}) — enforced after the grace period.",
    };

    public static string TicketComment(string? reason)
    {
        var headline = reason switch
        {
            "stale-lease-reaped" => "Reaped — the owning replica went away.",
            "watchdog-wall-time" => "Cancelled — exceeded the wall-time budget.",
            "budget" => "Cancelled — exceeded the cost/capacity budget.",
            "crashed" => "Cancelled — the run crashed.",
            "sandbox-vanished" => "Cancelled — the sandbox exited mid-run.",
            _ => "Cancelled by operator.",
        };
        return $"<b>Agent Smith — Cancelled</b><br/>{headline}";
    }
}
