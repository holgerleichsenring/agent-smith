namespace AgentSmith.Application.Services.Memory;

/// <summary>
/// 2026-11-10-fcd7a: a place memories live — a repository's .agentsmith/memory/ files or a project's rows. Code
/// that only needs entries reads them here, whichever store holds them.
/// </summary>
public interface IMemoryStore
{
    /// <summary>The store's index, one line per memory; null when it holds none.</summary>
    Task<string?> ReadIndexAsync(CancellationToken ct);

    Task<IReadOnlyList<MemoryEntry>> ListAsync(CancellationToken ct);

    Task<IReadOnlyList<MemoryEntry>> SearchAsync(string query, CancellationToken ct);

    /// <summary>Writes the entry by name; <paramref name="provenance"/> is kept where the store records it.</summary>
    Task UpsertAsync(MemoryEntry entry, CancellationToken ct, MemoryProvenance? provenance = null);
}

/// <summary>
/// 2026-11-10-fcd7a: where a memory came from — a conversation turn (SourceRef = its transcript index) or a
/// run's spec (SourceRef = the spec id), and who said or wrote it.
/// </summary>
public sealed record MemoryProvenance(string SourceKind, string SourceId, string? SourceRef, string? Author);
