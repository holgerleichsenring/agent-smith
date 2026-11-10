using AgentSmith.Contracts.Events;

namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-11-10-d854c: one page of a design turn's steps, run-wide and in order, with how many the run kept in all.
/// <paramref name="NextSeq"/> is the cursor the next page reads after; null on an empty page.
/// </summary>
public sealed record DialogActivityPage(int Count, IReadOnlyList<DialogActivityEvent> Steps, long? NextSeq, bool HasMore)
{
    /// <summary>2026-11-10-d854b: when the run's first and last kept steps happened — the turn's working span.</summary>
    public DateTimeOffset? FirstAt { get; init; }

    /// <inheritdoc cref="FirstAt"/>
    public DateTimeOffset? LastAt { get; init; }

    /// <summary>2026-11-10-d854b: how many of the steps spawned sub-agents.</summary>
    public int SubAgentCalls { get; init; }
}
