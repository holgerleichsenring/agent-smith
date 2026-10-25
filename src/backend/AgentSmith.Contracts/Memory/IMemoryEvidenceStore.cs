namespace AgentSmith.Contracts.Memory;

/// <summary>
/// 2026-10-25-9324a: the memory ledger — every fact about a memory's use, per repository and slug, kept apart
/// from the run so deleting a run deletes none of it. Empty, and writing nothing, where the composition has no
/// database.
/// </summary>
public interface IMemoryEvidenceStore
{
    /// <summary>Replaces the rows of <paramref name="kind"/> a spec of a run wrote with <paramref name="facts"/>.</summary>
    Task ReplaceAsync(
        string runId, string specId, string kind, IReadOnlyList<MemoryEvidenceFact> facts, CancellationToken cancellationToken);

    /// <summary>Keeps one outcome row — confirmed or strained — per run, spec and slug: the newest replaces any other.</summary>
    Task UpsertOutcomeAsync(MemoryEvidenceFact fact, CancellationToken cancellationToken);

    Task AppendAsync(MemoryEvidenceFact fact, CancellationToken cancellationToken);

    /// <summary>Every fact recorded for the repository, oldest first.</summary>
    Task<IReadOnlyList<MemoryEvidenceFact>> OfRepositoryAsync(string repoKey, CancellationToken cancellationToken);
}
