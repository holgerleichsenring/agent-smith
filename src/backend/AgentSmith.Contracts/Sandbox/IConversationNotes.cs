namespace AgentSmith.Contracts.Sandbox;

/// <summary>
/// 2026-11-12-cd4ck: a design conversation's working notes — one file the design partner reads and writes across
/// turns, as Claude Code keeps notes in a file across a session. Unbounded; every turn's prompt carries it whole.
/// Absent where there is no store.
/// </summary>
public interface IConversationNotes
{
    /// <summary>The conversation's notes, or null when none were written.</summary>
    Task<string?> ReadAsync(string sessionId, CancellationToken cancellationToken);

    /// <summary>Replaces the conversation's notes whole.</summary>
    Task WriteAsync(string sessionId, string notes, CancellationToken cancellationToken);
}
