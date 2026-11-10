using AgentSmith.Contracts.Memory;

namespace AgentSmith.Application.Services.Memory;

/// <summary>
/// 2026-11-10-fcd7a: the DB-free default — no project holds a memory, and a write says why it was not kept.
/// </summary>
public sealed class NullProjectMemoryRepository : IProjectMemoryRepository
{
    public const string NoStore = "this installation has no database, so a project memory cannot be kept";

    public bool IsAvailable => false;

    public Task<long?> ResolveProjectAsync(string project, CancellationToken cancellationToken) =>
        Task.FromResult<long?>(null);

    public Task<IReadOnlyList<ProjectMemoryRecord>> ListAsync(long projectId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ProjectMemoryRecord>>([]);

    public Task UpsertAsync(long projectId, ProjectMemoryRecord record, CancellationToken cancellationToken) =>
        throw new InvalidOperationException(NoStore);

    public Task<bool> DeleteAsync(long projectId, string slug, CancellationToken cancellationToken) =>
        Task.FromResult(false);
}
