namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// 2026-11-20-24bd: how many runs may use the agent's primary model deployment at once. A run holds its place for
/// the master and every sub-agent it starts; only another run waits. Null is no limit.
/// </summary>
public sealed partial class AgentConfig
{
    /// <summary>The config key the limit is set under, named by every message about it.</summary>
    public const string MaxConcurrentRunsKey = "agent.max_concurrent_runs";

    /// <summary>Runs that may use the primary deployment at once, via <c>agent.max_concurrent_runs</c>; null (the
    /// default) is unbounded. A shared deployment whose tokens-per-minute quota one run with its sub-agents fills
    /// sets 1.</summary>
    public int? MaxConcurrentRuns { get; set; }

    /// <summary>Why <paramref name="value"/> cannot be the limit (of agent <paramref name="agentName"/> when named), or
    /// null when it can. Below 1 no run could ever take a place, so every run on the deployment would wait forever.</summary>
    public static string? ConcurrencyProblem(int? value, string? agentName = null) =>
        value is < 1
            ? (agentName is null ? "" : $"Agent '{agentName}': ")
              + $"max_concurrent_runs is {value}; it must be at least 1, because it is how many runs may use the model "
              + $"deployment at once and below 1 no run could ever start ({MaxConcurrentRunsKey}; leave it unset for no limit)."
            : null;
}
