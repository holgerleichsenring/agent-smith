namespace AgentSmith.Contracts.WorkSpecs;

/// <summary>
/// p0390: what the master sends to <c>revise_work_spec</c>. Full-state
/// replacement of the revisable sections; the revision header and the read-only
/// done-section are the framework's, never the model's.
/// </summary>
public sealed record WorkSpecRevisionRequest(
    string Cause,
    string Goal,
    IReadOnlyList<string> Requirements,
    IReadOnlyList<string>? Constraints,
    IReadOnlyList<string>? Assumptions,
    IReadOnlyList<string>? Done);
