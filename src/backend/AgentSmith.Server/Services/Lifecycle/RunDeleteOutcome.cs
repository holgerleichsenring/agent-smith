namespace AgentSmith.Server.Services.Lifecycle;

/// <summary>p0337: the result of a single-run delete. PodTerminationFailed keeps
/// the record so the operator can retry — deleting a run whose pod is still live
/// (kill failed) would leave an untracked pod burning resources.</summary>
public enum RunDeleteOutcome
{
    Deleted,
    NotFound,
    PodTerminationFailed,
}
