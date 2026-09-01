namespace AgentSmith.Application.Services;

/// <summary>
/// 2026-09-01-b467: the message a run's checkpoint commit carries, in one place.
/// <para>
/// Two parties read it and they must not drift: <see cref="RepoWorkPusher"/> writes it when
/// it puts a repository's work on the branch, and the delivery diff finds the run's own
/// commits by it. The run id is the part that matters — a marker naming THIS run cannot be
/// confused with a commit anybody else made on the same branch.
/// </para>
/// </summary>
public static class RunCheckpointCommit
{
    public static string MessageFor(string runId) => $"[checkpoint] agent-smith run {runId}";
}
