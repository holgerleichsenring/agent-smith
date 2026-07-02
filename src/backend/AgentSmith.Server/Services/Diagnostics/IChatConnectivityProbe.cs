using AgentSmith.Contracts.Providers;

namespace AgentSmith.Server.Services.Diagnostics;

/// <summary>
/// Read-only auth probes for the chat adapters. Slack + Teams are always
/// registered, so "configured" means the platform's credentials are actually set;
/// unconfigured platforms are skipped (no row) rather than reported as failures.
/// </summary>
public interface IChatConnectivityProbe
{
    bool IsSlackConfigured { get; }

    bool IsTeamsConfigured { get; }

    Task<ConnectionProbeResult> ProbeSlackAsync(CancellationToken cancellationToken);

    Task<ConnectionProbeResult> ProbeTeamsAsync(CancellationToken cancellationToken);
}
