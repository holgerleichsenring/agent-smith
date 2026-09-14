namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-09-13-a284: whether a failed <c>git commit</c> failed because there was nothing to
/// commit — the one commit failure that is a normal outcome rather than a broken run.
/// <para>
/// Both commit sites read it off the message, and both carried their own copy of the same
/// two strings. Extracted when the target had to reach them, so what "empty" means is one
/// fact in one place instead of two that can disagree.
/// </para>
/// </summary>
public static class EmptyCommit
{
    public static bool Explains(Exception ex) =>
        ex is not null
        && (ex.Message.Contains("nothing to commit", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("no changes", StringComparison.OrdinalIgnoreCase));
}
