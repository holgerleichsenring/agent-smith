namespace AgentSmith.Contracts.Models.Lifecycle;

/// <summary>
/// 2026-10-09-af10: the subject of the work commit a failed run pushes. The persist writes it; the
/// PR sweep reads it, so a head that moved to our own work commit never counts as new.
/// </summary>
public static class WipCommit
{
    public const string Marker = "[wip] agent-smith run";

    public static string Subject(string runId) => $"{Marker} {runId}";

    public static bool IsOurs(string? message) =>
        message is not null && message.TrimStart().StartsWith(Marker, StringComparison.Ordinal);
}
