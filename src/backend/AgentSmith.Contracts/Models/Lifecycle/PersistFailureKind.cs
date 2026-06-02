namespace AgentSmith.Contracts.Models.Lifecycle;

/// <summary>
/// Categorises a PersistWorkBranchHandler per-repo outcome. NoChanges is a
/// clean skip (nothing staged — not a failure); AuthDenied / RemoteDivergent /
/// NetworkBlip / Unknown are real failures that flip the parent step red.
/// </summary>
public enum PersistFailureKind
{
    NoChanges,
    AuthDenied,
    RemoteDivergent,
    NetworkBlip,
    Unknown
}
