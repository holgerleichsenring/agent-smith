namespace AgentSmith.Contracts.Sandbox;

/// <summary>
/// 2026-10-02-075dd: what the design partner worked out about an upload — what it is, how to run
/// it, what it needs — kept as the set's one note, so the next turn, a run, or another model
/// follows the recipe instead of rediscovering it. Absent where there is no store.
/// </summary>
public interface IReferenceNotes
{
    /// <summary>The longest note a set keeps.</summary>
    public const int MaxChars = 8000;

    /// <summary>Every set's note of one conversation, by set id.</summary>
    Task<IReadOnlyDictionary<string, string>> NotesAsync(string sessionId, CancellationToken cancellationToken);

    /// <summary>Replaces one set's note; false when the conversation holds no such set.</summary>
    Task<bool> SetAsync(string sessionId, string setId, string note, CancellationToken cancellationToken);
}
