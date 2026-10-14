namespace AgentSmith.Contracts.Specs;

/// <summary>
/// 2026-10-13-8762j: the CLOSED list of kinds a contract name may have. A free-text kind let "event" and
/// "events" hold one number twice; the collision check is only as good as the key it collides on.
/// </summary>
public static class SeriesContractKinds
{
    public static readonly IReadOnlyList<string> All =
        ["event", "system-event", "table", "context-key", "endpoint", "config", "command", "tool", "type"];

    /// <summary>The kind as the list spells it, or null when it is not one of them.</summary>
    public static string? Known(string? kind) =>
        All.FirstOrDefault(k => string.Equals(k, kind?.Trim(), StringComparison.OrdinalIgnoreCase));
}
