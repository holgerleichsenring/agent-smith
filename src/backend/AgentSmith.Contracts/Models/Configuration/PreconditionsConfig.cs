namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// 2026-11-16-f3fbc: how often a ticket stuck on a precondition is checked again, from agentsmith.yml's top-level
/// <c>preconditions:</c> block. A settings save re-checks at once; an init lands by a person or a pipeline and
/// nothing reports it, so the interval is what notices it. Global: the check has no project dimension.
/// </summary>
public sealed class PreconditionsConfig
{
    /// <summary>The fewest seconds between two passes.</summary>
    public const int MinRecheckIntervalSeconds = 60;

    /// <summary>The seconds between two passes over the stuck tickets.</summary>
    public int RecheckIntervalSeconds { get; set; } = 300;

    /// <summary>What is wrong with these values, one sentence each; empty when nothing is. The ONE rule — the load and
    /// the settings store refuse what this names, never clamping it.</summary>
    public IReadOnlyList<string> Problems() =>
        RecheckIntervalSeconds >= MinRecheckIntervalSeconds
            ? []
            : [$"preconditions.recheck_interval_seconds is {RecheckIntervalSeconds}; it must be {MinRecheckIntervalSeconds} "
               + "or more, because each pass probes the stuck tickets' repositories over the host API, which rate-limits"];
}
