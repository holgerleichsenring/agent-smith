namespace AgentSmith.Contracts.Runs;

/// <summary>
/// 2026-10-08-0781: who asked the worker to check a ticket. The values rank a merge — a ticket's
/// own comment outranks a pull-request review, which outranks a run end or a sweep.
/// </summary>
public enum ReworkNudgeOrigin
{
    Sweep = 1,
    RunEnd = 2,
    PullRequest = 3,
    Ticket = 4,
}
