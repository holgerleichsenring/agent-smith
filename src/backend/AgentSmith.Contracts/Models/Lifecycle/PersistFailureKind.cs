namespace AgentSmith.Contracts.Models.Lifecycle;

/// <summary>
/// Categorises why a PersistWorkBranchHandler invocation failed.
/// NetworkBlip is transient; everything else accumulates in PersistFailureCounter
/// and escalates after the configured threshold.
/// </summary>
public enum PersistFailureKind
{
    NoChanges,
    AuthDenied,
    RemoteDivergent,
    NetworkBlip,
    Unknown
}
