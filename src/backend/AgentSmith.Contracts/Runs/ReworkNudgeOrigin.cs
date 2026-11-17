namespace AgentSmith.Contracts.Runs;

/// <summary>
/// 2026-10-08-0781: who asked the worker to check a ticket. The values rank a merge — a ticket's
/// own comment outranks a pull-request review, which outranks a run end or a sweep.
/// <para>2026-10-13-9a59b: <see cref="Refresh"/> ranks below everything — it asks only that the
/// ticket's pull-request records be read again, so any other origin merged with it wins and a
/// Refresh alone never reaches the rework handler.</para>
/// </summary>
public enum ReworkNudgeOrigin
{
    Refresh = 0,
    Sweep = 1,
    RunEnd = 2,
    PullRequest = 3,
    Ticket = 4,

    /// <summary>2026-10-13-9a59m: an approval in the ticket's own conversation — a person's act, never late.</summary>
    Operator = 5,

    /// <summary>2026-10-14-dbceh: the last ticket it requires landed. Served only for a ticket without an
    /// attempt, which it starts; it ranks above every origin so a merge keeps the start.</summary>
    RequirementsMet = 6,

    /// <summary>2026-11-16-f3fbc: the precondition a ticket's newest attempt stopped on now holds. A pending act is
    /// still served as rework; otherwise the ticket is released. It ranks above every origin so a merge keeps it.</summary>
    PreconditionsMet = 7,
}
