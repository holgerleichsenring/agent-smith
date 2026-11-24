using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

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
    public static string Summary(string? reason, string? pipeline = null) => reason switch
    {
        "stale-lease-reaped" =>
            "Reaped — the owning replica went away (stale lease); cancelled after the grace period.",
        // 2026-11-23-c81ec: the key that applied to the run's kind.
        "watchdog-wall-time" => PipelinePresets.RunKindOf(pipeline ?? string.Empty) == RegisteredRunKind.Poc
            ? $"Cancelled — the run exceeded its wall-time budget ({PocsConfig.RunWallTimeSecondsKey})."
            : $"Cancelled — the run exceeded its wall-time budget ({RunWallTimeCeiling.OrchestratorKey}).",
        "budget" =>
            "Cancelled — the run exceeded its cost/capacity budget.",
        "crashed" =>
            "Cancelled — the run crashed and could not continue.",
        // p0396: no OOM guess — the exit-evidence-based sentence lives on the run
        // summary (SandboxVanishSummary); this row only names the cause category.
        "sandbox-vanished" =>
            "Cancelled — the sandbox container exited mid-run (see the sandbox container log for the exit cause).",
        // 2026-10-02-5f89e: RunLivenessReaper — the row's heartbeat stopped.
        "interrupted" =>
            "Interrupted — no server was driving this run any more (restart or crash).",
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
            "interrupted" => "Interrupted — no server was driving this run any more.",
            _ => "Cancelled by operator.",
        };
        return $"<b>Agent Smith — Cancelled</b><br/>{headline}";
    }
}
