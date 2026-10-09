namespace AgentSmith.Contracts.Reviews;

/// <summary>2026-10-08-f147: the opening of every comment agent-smith posts on a pull request. It
/// makes a note ours only together with the token's own account.</summary>
public static class OwnPrNoteMarker
{
    public const string Prefix = "<!-- agentsmith:";

    public static bool IsOurs(string? body, bool writtenByToken) =>
        writtenByToken && body?.StartsWith(Prefix, StringComparison.Ordinal) == true;
}
