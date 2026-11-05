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
    /// <summary>2026-11-04-6768a: the part every run's checkpoint shares, so a reader can find the
    /// series' work across runs without knowing their ids.</summary>
    public const string Marker = "[checkpoint] agent-smith run";

    public static string MessageFor(string runId) => $"{Marker} {runId}";
}
