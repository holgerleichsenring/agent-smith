namespace AgentSmith.Application.Models.Registry;

/// <summary>
/// Result of guarding, substituting and writing one staged auth file.
/// <see cref="Hosts"/> are the placeholder hosts the file's content referenced;
/// on failure, <see cref="FailureReason"/> feeds the loud per-host decision
/// line ("registry auth NOT staged for host X: reason").
/// </summary>
public sealed record StagedFileOutcome(
    bool Written, string? WrittenPath, IReadOnlyList<string> Hosts, string? FailureReason)
{
    public static StagedFileOutcome Ok(string writtenPath, IReadOnlyList<string> hosts) =>
        new(true, writtenPath, hosts, null);
    public static StagedFileOutcome Fail(IReadOnlyList<string> hosts, string reason) =>
        new(false, null, hosts, reason);
}
