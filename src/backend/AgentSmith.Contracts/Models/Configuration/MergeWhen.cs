namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>2026-11-17-023fa: when a code run merges its pull requests. Values are stored as numbers.</summary>
public enum MergeWhen
{
    /// <summary>Pull requests stay open for a person.</summary>
    Manual = 0,

    /// <summary>A run whose every spec verified green merges its pull requests at its end.</summary>
    OnGateGreen = 1,

    /// <summary>2026-11-17-3435b: a gate-passed run's pull requests wait for the host's CI; green on every one merges them.</summary>
    OnCiGreen = 2,
}
