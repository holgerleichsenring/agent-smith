using AgentSmith.Contracts.Memory;

namespace AgentSmith.Application.Services.Memory;

/// <summary>2026-10-25-9324a: the DB-free default — nothing is kept, so every memory reads as without a record.</summary>
public sealed class NullMemoryEvidenceStore : IMemoryEvidenceStore
{
    public Task ReplaceAsync(
        string runId, string specId, string kind, IReadOnlyList<MemoryEvidenceFact> facts,
        CancellationToken cancellationToken) => Task.CompletedTask;

    public Task UpsertOutcomeAsync(MemoryEvidenceFact fact, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task AppendAsync(MemoryEvidenceFact fact, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<IReadOnlyList<MemoryEvidenceFact>> OfRepositoryAsync(string repoKey, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<MemoryEvidenceFact>>([]);
}
