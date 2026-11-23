namespace AgentSmith.Contracts.Providers;

/// <summary>2026-11-23-933de: what a registry probe found.</summary>
public enum RegistryProbeOutcome
{
    /// <summary>The pull token may pull under the repository prefix.</summary>
    Reached,

    /// <summary>The token signs in, but its scope map leaves the prefix out.</summary>
    ScopeMissing,

    /// <summary>The registry refused the pull token itself.</summary>
    TokenRefused,
}
