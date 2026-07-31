namespace AgentSmith.Contracts.WorkSpecs;

/// <summary>
/// p0390: the outcome of committing one revision. A failure is NOT fatal to the
/// run: the spec is additional context, never a gate, so a repo that refuses the
/// push leaves the run working from the ticket exactly as it does today.
/// </summary>
public sealed record WorkSpecWriteResult(bool Written, string? CommitSha, string? Error)
{
    public static WorkSpecWriteResult Ok(string sha) => new(true, sha, null);

    public static WorkSpecWriteResult Failed(string error) => new(false, null, error);
}
