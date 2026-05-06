using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Providers;

/// <summary>
/// Clones a remote source repository to a host filesystem path. Used by api-scan's
/// fail-soft source resolution where the analyzers (RouteMapper / extractors) read
/// from host disk via System.IO. Distinct from the sandbox-routed git clone Step
/// driven by CheckoutSourceHandler for AgenticExecute / Test pipelines.
/// </summary>
public interface IHostSourceCloner
{
    /// <summary>
    /// Clones the source to a fresh tempdir on the host filesystem and returns the
    /// path. Returns null on any failure (network, missing git binary, auth, …)
    /// so callers can fall back to passive mode without unwrapping exceptions.
    /// </summary>
    Task<string?> TryCloneAsync(SourceConfig source, CancellationToken cancellationToken);
}
