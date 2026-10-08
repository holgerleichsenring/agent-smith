namespace AgentSmith.Contracts.Sandbox;

/// <summary>
/// 2026-10-01-283dc: the files of one uploaded website, read when its sandbox is first filled.
/// A composition with no durable store has no sets and reads none.
/// </summary>
public interface IReferenceSetReader
{
    Task<IReadOnlyList<ReferenceSetFile>> FilesAsync(
        string sessionId, string setId, CancellationToken cancellationToken);

    /// <summary>
    /// 2026-10-01-283df: the ids of the website sets a conversation holds, oldest first — what an
    /// approval cites. Empty where there is no store.
    /// </summary>
    Task<IReadOnlyList<string>> SetIdsAsync(string sessionId, CancellationToken cancellationToken);

    /// <summary>
    /// 2026-10-08-e8b9j: one file of a set by its path, or null — what view_reference_image shows.
    /// The default reads the set whole; the relational reader reads the one row.
    /// </summary>
    async Task<ReferenceSetFile?> FileAsync(
        string sessionId, string setId, string path, CancellationToken cancellationToken) =>
        (await FilesAsync(sessionId, setId, cancellationToken)).FirstOrDefault(f => f.Path == path);
}
