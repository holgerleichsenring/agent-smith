namespace AgentSmith.Contracts.Runs;

/// <summary>
/// 2026-11-17-166aa: the ends that leave a ticket in its tracker status when the project configures no
/// failed_status — a failure, or a cancel that was not a lost process (<see cref="OwedWorkCancel"/>). Such a ticket
/// still sits in a status the poller claims, so the claim door holds it until a person starts it again.
/// </summary>
public static class LeftInPlaceEnd
{
    public const string Cancelled = "cancelled";

    public static bool Ends(string? status, string? cancelReason) =>
        status == RunStatuses.Failed || (status == Cancelled && !OwedWorkCancel.IsOwed(cancelReason));
}
