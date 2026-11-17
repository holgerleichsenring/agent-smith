namespace AgentSmith.Contracts.Runs;

/// <summary>
/// 2026-11-16-f3fbc: why agent-smith starts a ticket's attempt through the chat funnel — the marker the spawn
/// request seeds, so the attempt's cause reads it. Replaces the series flag the launch carried.
/// </summary>
public enum ReworkStartKind
{
    /// <summary>A person's act: a start by name, a rework act, a retry.</summary>
    Act = 0,

    /// <summary>2026-10-13-8762v: the series' next attempt.</summary>
    Series = 1,

    /// <summary>2026-11-16-f3fbc: the precondition the previous attempt stopped on now holds.</summary>
    PreconditionRelease = 2,
}
