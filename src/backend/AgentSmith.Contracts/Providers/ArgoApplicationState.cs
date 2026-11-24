namespace AgentSmith.Contracts.Providers;

/// <summary>2026-11-23-c81ec: an Argo CD Application's status as its API answers it — sync status ('Synced',
/// 'OutOfSync', …), health status ('Healthy', 'Progressing', 'Degraded', …) with its message, and how its last operation
/// stands ('Succeeded', 'Running', 'Failed', 'Error') with its message, each null when absent.</summary>
public sealed record ArgoApplicationState(
    string? SyncStatus, string? HealthStatus, string? HealthMessage, string? OperationPhase, string? OperationMessage)
{
    public const string Synced = "Synced";
    public const string Healthy = "Healthy";
    public const string Degraded = "Degraded";

    /// <summary>Whether the last operation ended failed or in error.</summary>
    public bool OperationFailed => OperationPhase is "Failed" or "Error";
}
