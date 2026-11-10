using AgentSmith.Contracts.Memory;

namespace AgentSmith.Application.Services.Memory;

/// <summary>
/// 2026-11-10-fcd7a: a project's memories as an <see cref="IMemoryStore"/>. The rows are the store; the index is
/// composed from them, newest first. A write without provenance is recorded as coming from nowhere in
/// particular, which no caller is meant to do.
/// </summary>
public sealed class ProjectMemoryStore(IProjectMemoryRepository repository, long projectId) : IMemoryStore
{
    public long ProjectId => projectId;

    public async Task<string?> ReadIndexAsync(CancellationToken ct)
    {
        var entries = await ListAsync(ct);
        return entries.Count == 0
            ? null
            : string.Join('\n', entries.Select(MemoryIndexUpdater.FormatLine));
    }

    public async Task<IReadOnlyList<MemoryEntry>> ListAsync(CancellationToken ct) =>
        [.. (await repository.ListAsync(projectId, ct)).Select(EntryOf)];

    public async Task<IReadOnlyList<MemoryEntry>> SearchAsync(string query, CancellationToken ct) =>
        [.. (await ListAsync(ct)).Where(e => MemoryQueryMatcher.Matches(e, query))];

    public Task UpsertAsync(MemoryEntry entry, CancellationToken ct, MemoryProvenance? provenance = null)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var source = provenance ?? new MemoryProvenance(ProjectMemorySourceKinds.Run, string.Empty, null, null);
        return repository.UpsertAsync(projectId, new ProjectMemoryRecord(
            entry.Name, MemoryEntryTypes.ToSlug(entry.Type), entry.Description, entry.Body,
            source.SourceKind, source.SourceId, source.SourceRef, source.Author), ct);
    }

    private static MemoryEntry EntryOf(ProjectMemoryRecord record) => new(
        record.Slug, record.Description,
        MemoryEntryTypes.TryParse(record.Type, out var type) ? type : MemoryEntryType.Reference, record.Body);
}
