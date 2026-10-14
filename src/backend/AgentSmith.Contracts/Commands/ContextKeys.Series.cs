namespace AgentSmith.Contracts.Commands;

/// <summary>2026-10-13-8762v: the key a series' automatic start rides in a run's initial context.</summary>
public static partial class ContextKeys
{
    /// <summary>2026-10-13-8762v: true when agent-smith started this attempt itself to continue the ticket's spec
    /// series (bool, or a JSON true after the capacity queue's round-trip). Seeded only by the spawn funnel; read by
    /// the attempt's cause and the boundary claim gate.</summary>
    public const string SeriesAutoStart = "SeriesAutoStart";
}
