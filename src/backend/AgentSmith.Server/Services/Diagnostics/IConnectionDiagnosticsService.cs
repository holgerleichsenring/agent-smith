namespace AgentSmith.Server.Services.Diagnostics;

/// <summary>
/// On-demand connectivity diagnostics for the dashboard. The snapshot lists
/// probeable connections and the webhook panel WITHOUT any outbound call;
/// <see cref="ProbeAsync"/> performs the read-only round-trip for one connection
/// when the operator asks.
/// </summary>
public interface IConnectionDiagnosticsService
{
    Task<ConnectionDiagnosticsSnapshot> GetSnapshotAsync(CancellationToken cancellationToken);

    /// <summary>Probes a single connection by catalog name; null when unknown.</summary>
    Task<ConnectionStatus?> ProbeAsync(string name, CancellationToken cancellationToken);
}
