namespace AgentSmith.Contracts.Models.Lifecycle;

/// <summary>
/// 2026-10-09-af10: the subject of the work commit a failed run pushes.
/// </summary>
public static class WipCommit
{
    public const string Marker = "[wip] agent-smith run";

    public static string Subject(string runId) => $"{Marker} {runId}";
}
