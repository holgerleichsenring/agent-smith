using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Providers;

/// <summary>
/// Clones a remote source repository to a host filesystem path. Used by api-scan's
/// fail-soft source resolution so skills can read project sources from host disk via
/// their Read/Grep tools. Distinct from the sandbox-routed git clone driven by
/// CheckoutSourceHandler for AgenticExecute / Test pipelines.
/// </summary>
public interface IHostSourceCloner
{
    /// <summary>
    /// Clones the source to a fresh tempdir on the host filesystem and returns the
    /// path. Returns null on any failure (network, missing git binary, auth, …)
    /// so callers can fall back to passive mode without unwrapping exceptions.
    /// </summary>
    Task<string?> TryCloneAsync(RepoConnection source, CancellationToken cancellationToken);
}
